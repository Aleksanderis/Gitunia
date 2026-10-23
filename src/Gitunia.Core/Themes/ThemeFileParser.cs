using System.Globalization;
using System.Text.Json;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Themes.Models;

namespace Gitunia.Core.Themes;

/// <summary>
/// Reads a theme file (format: <c>docs/ux.md</c>, "Theme files"). Pure: the text comes in, the theme and the reasons
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

    private static string ReadName(JsonElement root, string fallbackName) =>
        root.TryGetProperty(NameKey, out var name) && name.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(name.GetString())
            ? name.GetString()!.Trim()
            : fallbackName;

    private static bool? ReadVariant(string file, JsonElement root, ICollection<ConfigProblem> problems)
    {
        var text = root.TryGetProperty(VariantKey, out var variant) && variant.ValueKind == JsonValueKind.String
            ? variant.GetString()?.Trim().ToLowerInvariant()
            : null;
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
            problems.Add(new ConfigProblem(file, $"{ColorsKey} must be an object of role names to \"#rrggbb\" colours"));
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

    private static void FillDefaults(Dictionary<string, int> colors)
    {
        colors.TryAdd(ThemeRoles.MutedOnSelection, colors[ThemeRoles.Muted]);
        colors.TryAdd(ThemeRoles.TabButtonBackground, colors[ThemeRoles.SelectionFocused]);
        colors.TryAdd(ThemeRoles.ChipBackground, colors[ThemeRoles.Accent]);
        colors.TryAdd(ThemeRoles.MatchForeground, colors[ThemeRoles.ChipForeground]);
        colors.TryAdd(ThemeRoles.MatchBackground, colors[ThemeRoles.Warn]);
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
            var colorName = property.Value.ValueKind == JsonValueKind.String
                ? AnsiColorNames.Find(property.Value.GetString() ?? string.Empty)
                : null;
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

        return int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
