using System.Globalization;
using System.Text.Json;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes.Models;

namespace Gitunia.Core.Themes;

/// <summary>
/// Reads a theme file (format: <c>docs/ux-look.md</c>, "Theme files"). Pure: the text comes in, the theme and the reasons
/// it was refused come out. Uses the DOM reader of System.Text.Json, so it needs no reflection.
/// </summary>
public static class ThemeFileParser
{
    /// <summary>The only <c>version</c> there is.</summary>
    public const int CurrentVersion = 1;

    private const string VersionKey = "version";
    private const string NameKey = "name";
    private const string VariantKey = "variant";
    private const string ColorsKey = "colors";
    private const string LanesKey = "lanes";
    private const string AnsiKey = "ansi";
    private const string LightVariant = "light";
    private const string DarkVariant = "dark";
    private const string ColorHint = "must be a colour like \"#1e1e2e\"";
    private const int HexLength = 7;

    // The share of the text colour kept when it is blended into the background it sits on.
    private const double SubtleWeight = 0.55;
    private const double SubtleOnSelectionWeight = 0.7;
    private const double WordWeight = 0.15;
    private const double WordWeightStep = 0.01;
    private const double WordContrast = 4.8;
    private const double ContrastOffset = 0.05;
    private const double RedLuminance = 0.2126;
    private const double GreenLuminance = 0.7152;
    private const double BlueLuminance = 0.0722;
    private const double LinearThreshold = 0.03928;
    private const double LinearDivisor = 12.92;
    private const double ChannelMaximum = 255.0;
    private const double GammaOffset = 0.055;
    private const double GammaScale = 1.055;
    private const double GammaExponent = 2.4;
    private static readonly int[] ChannelShifts = [16, 8, 0];
    private const char HexPrefix = '#';

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The theme in a file's text, or null (with problems added) when the file cannot be used.</summary>
    /// <param name="fallbackName">The name to use when the file has none (its file name).</param>
    /// <param name="text">The text of the file.</param>
    /// <param name="problems">Receives one problem per reason; the path of each starts with the file name.</param>
    public static ThemeDefinition? Parse(string fallbackName, string text, ICollection<ConfigProblem> problems)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Options);
        }
        catch (JsonException exception)
        {
            problems.Add(new ConfigProblem(fallbackName, $"theme file is not valid JSON: {exception.Message}"));
            return null;
        }

        using (document)
        {
            return Read(fallbackName, document.RootElement, problems);
        }
    }

    private static ThemeDefinition? Read(string fallbackName, JsonElement root, ICollection<ConfigProblem> problems)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new ConfigProblem(fallbackName, "theme file must be a JSON object"));
            return null;
        }

        var before = problems.Count;
        CheckVersion(fallbackName, root, problems);
        var name = ReadName(root, fallbackName);
        var isLight = ReadVariant(fallbackName, root, problems);
        var colors = ReadColors(fallbackName, root, problems);
        var lanes = ReadLanes(fallbackName, root, problems);
        var ansi = ReadAnsi(fallbackName, root, problems);
        if (problems.Count > before || isLight is null || colors is null || lanes is null)
        {
            return null;
        }

        return new ThemeDefinition(name, isLight.Value, colors, lanes, ansi);
    }

    private static void CheckVersion(string file, JsonElement root, ICollection<ConfigProblem> problems)
    {
        if (!root.TryGetProperty(VersionKey, out var version))
        {
            return;
        }

        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number)
            || number != CurrentVersion)
        {
            problems.Add(new ConfigProblem(file, $"{VersionKey} must be {CurrentVersion} (the only format there is)"));
        }
    }

    private static string ReadName(JsonElement root, string fallbackName)
    {
        if (root.TryGetProperty(NameKey, out var name)
            && name.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(name.GetString()))
        {
            return name.GetString()!.Trim();
        }

        return fallbackName;
    }

    private static bool? ReadVariant(string file, JsonElement root, ICollection<ConfigProblem> problems)
    {
        string? text = null;
        if (root.TryGetProperty(VariantKey, out var variant) && variant.ValueKind == JsonValueKind.String)
        {
            text = variant.GetString()?.Trim().ToLowerInvariant();
        }

        switch (text)
        {
            case LightVariant:
                return true;
            case DarkVariant:
                return false;
            default:
                problems.Add(new ConfigProblem(file, $"{VariantKey} must be \"{DarkVariant}\" or \"{LightVariant}\""));
                return null;
        }
    }

    private static Dictionary<string, int>? ReadColors(
        string file,
        JsonElement root,
        ICollection<ConfigProblem> problems)
    {
        if (!root.TryGetProperty(ColorsKey, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            var text = $"{ColorsKey} must be an object of role names to \"#rrggbb\" colours";
            problems.Add(new ConfigProblem(file, text));
            return null;
        }

        var colors = new Dictionary<string, int>();
        var failed = false;
        foreach (var role in ThemeRoles.Required)
        {
            var path = $"{file}: {ColorsKey}.{role}";
            if (!section.TryGetProperty(role, out var element))
            {
                problems.Add(new ConfigProblem(path, "is missing"));
                failed = true;
            }
            else if (ParseHex(element) is { } value)
            {
                colors[role] = value;
            }
            else
            {
                problems.Add(new ConfigProblem(path, ColorHint));
                failed = true;
            }
        }

        foreach (var role in ThemeRoles.Optional)
        {
            if (!section.TryGetProperty(role, out var element))
            {
                continue;
            }

            if (ParseHex(element) is { } value)
            {
                colors[role] = value;
            }
            else
            {
                problems.Add(new ConfigProblem($"{file}: {ColorsKey}.{role}", ColorHint));
                failed = true;
            }
        }

        if (failed)
        {
            return null;
        }

        FillDefaults(colors);

        return colors;
    }

    private static int Mix(int foreground, int background, double weight)
    {
        var result = 0;
        foreach (var shift in ChannelShifts)
        {
            var channel = (((foreground >> shift) & 0xff) * weight) + (((background >> shift) & 0xff) * (1 - weight));
            result |= (int)Math.Round(channel) << shift;
        }

        return result;
    }

    // The background of changed words: the text colour blended into the base background, as strongly as the text
    // stays readable on it (a light theme, whose text is barely dark enough, gets a faint tint).
    private static int WordTint(int foreground, int background)
    {
        var weight = WordWeight;
        var tint = Mix(foreground, background, weight);
        while (weight > WordWeightStep && Contrast(foreground, tint) < WordContrast)
        {
            weight -= WordWeightStep;
            tint = Mix(foreground, background, weight);
        }

        return tint;
    }

    private static double Contrast(int first, int second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));

        return (lighter + ContrastOffset) / (darker + ContrastOffset);
    }

    private static double Luminance(int color) =>
        (RedLuminance * Linear((color >> 16) & 0xff))
        + (GreenLuminance * Linear((color >> 8) & 0xff))
        + (BlueLuminance * Linear(color & 0xff));

    private static double Linear(int channel)
    {
        var value = channel / ChannelMaximum;

        if (value <= LinearThreshold)
        {
            return value / LinearDivisor;
        }

        return Math.Pow((value + GammaOffset) / GammaScale, GammaExponent);
    }

    private static void FillDefaults(Dictionary<string, int> colors)
    {
        colors.TryAdd(ThemeRoles.MutedOnSelection, colors[ThemeRoles.Muted]);
        colors.TryAdd(
            ThemeRoles.Subtle,
            Mix(colors[ThemeRoles.Muted], colors[ThemeRoles.BaseBackground], SubtleWeight));
        colors.TryAdd(
            ThemeRoles.SubtleOnSelection,
            Mix(colors[ThemeRoles.MutedOnSelection], colors[ThemeRoles.SelectionFocused], SubtleOnSelectionWeight));
        colors.TryAdd(ThemeRoles.TabButtonBackground, colors[ThemeRoles.SelectionFocused]);
        colors.TryAdd(ThemeRoles.ChipBackground, colors[ThemeRoles.Accent]);
        colors.TryAdd(ThemeRoles.MatchForeground, colors[ThemeRoles.ChipForeground]);
        colors.TryAdd(ThemeRoles.MatchBackground, colors[ThemeRoles.Warn]);
        colors.TryAdd(
            ThemeRoles.AddedWord,
            WordTint(colors[ThemeRoles.Good], colors[ThemeRoles.BaseBackground]));
        colors.TryAdd(
            ThemeRoles.RemovedWord,
            WordTint(colors[ThemeRoles.Bad], colors[ThemeRoles.BaseBackground]));
    }

    private static List<int>? ReadLanes(string file, JsonElement root, ICollection<ConfigProblem> problems)
    {
        if (!root.TryGetProperty(ColorsKey, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var path = $"{file}: {ColorsKey}.{LanesKey}";
        if (!section.TryGetProperty(LanesKey, out var array) || array.ValueKind != JsonValueKind.Array
            || array.GetArrayLength() == 0)
        {
            problems.Add(new ConfigProblem(path, "must be a non-empty list of colours"));
            return null;
        }

        var lanes = new List<int>();
        foreach (var element in array.EnumerateArray())
        {
            if (ParseHex(element) is not { } value)
            {
                problems.Add(new ConfigProblem($"{path}[{lanes.Count}]", ColorHint));
                return null;
            }

            lanes.Add(value);
        }

        return lanes;
    }

    private static Dictionary<string, string> ReadAnsi(
        string file,
        JsonElement root,
        ICollection<ConfigProblem> problems)
    {
        var ansi = new Dictionary<string, string>();
        if (!root.TryGetProperty(AnsiKey, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return ansi;
        }

        foreach (var property in section.EnumerateObject())
        {
            var path = $"{file}: {AnsiKey}.{property.Name}";
            var role = ThemeRoles.AnsiRoles.FirstOrDefault(
                candidate => string.Equals(candidate, property.Name, StringComparison.OrdinalIgnoreCase));
            string? colorName = null;
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                colorName = AnsiColorNames.Find(property.Value.GetString() ?? string.Empty);
            }

            if (role is null)
            {
                var roles = string.Join(", ", ThemeRoles.AnsiRoles);
                problems.Add(new ConfigProblem(path, $"is not a role with a 16-colour mapping ({roles})"));
            }
            else if (colorName is null)
            {
                problems.Add(new ConfigProblem(path, $"must be one of {string.Join(", ", AnsiColorNames.All)}"));
            }
            else
            {
                ansi[role] = colorName;
            }
        }

        return ansi;
    }

    private static int? ParseHex(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = element.GetString() ?? string.Empty;
        if (text.Length != HexLength || text[0] != HexPrefix)
        {
            return null;
        }

        if (int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }
}
