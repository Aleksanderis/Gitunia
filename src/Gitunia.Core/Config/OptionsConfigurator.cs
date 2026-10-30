using System.Collections.Immutable;
using System.Text.Json;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Reads <c>tabs.showBranch</c> and <c>scanRoots</c>; a bad value keeps its default and adds
/// a problem.
/// </summary>
public static class OptionsConfigurator
{
    private const string TabsKey = "tabs";
    private const string ShowBranchKey = "tabs.showBranch";
    private const string ScanRootsKey = "scanRoots";
    private const string WatchFilesKey = "watchFiles";
    private const string LabelCapsKey = "labelCaps";
    private const string CommandLevelKey = "commandLevel";
    private const string HintsKey = "hints";
    private const string WhichKeyKey = "hints.whichKey";
    private const string StatusBarKey = "hints.statusBar";
    private const string WhichKeyProperty = "whichKey";
    private const string StatusBarProperty = "statusBar";
    private const string ShowBranchProperty = "showBranch";
    private const char HomeMarker = '~';

    /// <summary>Validates the options.</summary>
    /// <param name="document">The parsed file.</param>
    /// <param name="homeDirectory">What a leading <c>~</c> in a scan root stands for.</param>
    /// <param name="directoryExists">Whether a directory exists (injected, so this stays pure).</param>
    /// <param name="problems">Receives one problem per bad value.</param>
    public static ConfigOptions Read(
        ConfigDocument document,
        string homeDirectory,
        Func<string, bool> directoryExists,
        ICollection<ConfigProblem> problems) =>
        new(
            ParseShowBranch(document.Tabs, problems),
            ParseScanRoots(document.ScanRoots, homeDirectory, directoryExists, problems),
            ParseWatchFiles(document.WatchFiles, problems),
            ParseLabelCaps(document.LabelCaps, problems),
            ParseWhichKey(document.Hints, problems),
            ParseStatusBar(document.Hints, problems),
            ParseCommandLevel(document.CommandLevel, problems));

    /// <summary>
    /// The <c>commandLevel</c> option (#839): everyday (default), advanced or advanced+, any case; anything else is
    /// reported and gives everyday.
    /// </summary>
    public static CommandLevel ParseCommandLevel(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return CommandLevels.Default;
        }

        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (CommandLevels.TryParse(text, out var level))
        {
            return level;
        }

        problems.Add(new ConfigProblem(
            CommandLevelKey,
            "commandLevel must be everyday, advanced or advanced+, using everyday"));

        return CommandLevels.Default;
    }

    /// <summary>The <c>hints.whichKey</c> option: on (default) or off, any case; anything else is reported and gives on.</summary>
    public static bool ParseWhichKey(JsonElement? hints, ICollection<ConfigProblem> problems)
    {
        var text = HintText(hints, WhichKeyProperty, problems, out var present);
        if (!present || text == "on")
        {
            return true;
        }

        if (text == "off")
        {
            return false;
        }

        problems.Add(new ConfigProblem(WhichKeyKey, "hints.whichKey must be on or off, using on"));

        return true;
    }

    /// <summary>The <c>hints.statusBar</c> option: full (default), minimal or off, any case; anything else is reported and gives full.</summary>
    public static StatusBarHintMode ParseStatusBar(JsonElement? hints, ICollection<ConfigProblem> problems)
    {
        var text = HintText(hints, StatusBarProperty, problems, out var present);
        if (!present || text == "full")
        {
            return StatusBarHintMode.Full;
        }

        if (text == "minimal")
        {
            return StatusBarHintMode.Minimal;
        }

        if (text == "off")
        {
            return StatusBarHintMode.Off;
        }

        problems.Add(new ConfigProblem(StatusBarKey, "hints.statusBar must be full, minimal or off, using full"));

        return StatusBarHintMode.Full;
    }

    // The lower-case text of one member of the hints object; present is false when the member (or the object) is
    // absent or null, and the text is empty when it is not a string. A hints value that is no object is reported once,
    // by the which-key read.
    private static string HintText(JsonElement? hints, string property, ICollection<ConfigProblem> problems, out bool present)
    {
        present = false;
        if (hints is null or { ValueKind: JsonValueKind.Null })
        {
            return string.Empty;
        }

        if (hints.Value.ValueKind != JsonValueKind.Object)
        {
            if (property == WhichKeyProperty)
            {
                problems.Add(new ConfigProblem(HintsKey, "hints must be an object, using the defaults"));
            }

            return string.Empty;
        }

        var text = string.Empty;
        foreach (var member in hints.Value.EnumerateObject())
        {
            if (!string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase)
                || member.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            present = true;
            text = member.Value.ValueKind == JsonValueKind.String
                ? member.Value.GetString()?.ToLowerInvariant() ?? string.Empty
                : string.Empty;
        }

        return text;
    }

    /// <summary>The <c>labelCaps</c> option: none, rounded or angled (any case); no value or anything else gives rounded.</summary>
    public static LabelCapStyle ParseLabelCaps(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return LabelCapStyle.Rounded;
        }

        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() : null;
        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))
        {
            return LabelCapStyle.None;
        }

        if (string.Equals(text, "rounded", StringComparison.OrdinalIgnoreCase))
        {
            return LabelCapStyle.Rounded;
        }

        if (string.Equals(text, "angled", StringComparison.OrdinalIgnoreCase))
        {
            return LabelCapStyle.Angled;
        }

        problems.Add(new ConfigProblem(LabelCapsKey, "labelCaps must be none, rounded or angled, using rounded"));

        return LabelCapStyle.Rounded;
    }

    /// <summary>The <c>watchFiles</c> flag; anything but true or false gives false.</summary>
    public static bool ParseWatchFiles(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return false;
        }

        if (value.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.Value.GetBoolean();
        }

        problems.Add(new ConfigProblem(WatchFilesKey, "watchFiles must be true or false, using false"));

        return false;
    }

    /// <summary>The <c>tabs.showBranch</c> flag from the <c>tabs</c> object; anything else gives false.</summary>
    public static bool ParseShowBranch(JsonElement? tabs, ICollection<ConfigProblem> problems)
    {
        if (tabs is null or { ValueKind: JsonValueKind.Null })
        {
            return false;
        }

        if (tabs.Value.ValueKind != JsonValueKind.Object)
        {
            problems.Add(new ConfigProblem(TabsKey, "tabs must be an object, using the defaults"));
            return false;
        }

        foreach (var property in tabs.Value.EnumerateObject())
        {
            if (!string.Equals(property.Name, ShowBranchProperty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return property.Value.GetBoolean();
            }

            if (property.Value.ValueKind != JsonValueKind.Null)
            {
                problems.Add(new ConfigProblem(ShowBranchKey, "tabs.showBranch must be true or false, using false"));
            }
        }

        return false;
    }

    /// <summary>
    /// The scan roots: absolute paths or <c>~</c>-relative ones that exist. Entries that cannot be used are reported
    /// and skipped; when none of a non-empty list is usable the conventional folders apply. An empty list turns
    /// scanning off.
    /// </summary>
    public static ImmutableArray<string>? ParseScanRoots(
        JsonElement? value,
        string homeDirectory,
        Func<string, bool> directoryExists,
        ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return null;
        }

        if (value.Value.ValueKind != JsonValueKind.Array)
        {
            var message = "scanRoots must be a list of folders, using the default folders";
            problems.Add(new ConfigProblem(ScanRootsKey, message));
            return null;
        }

        var roots = ImmutableArray.CreateBuilder<string>();
        var index = 0;
        foreach (var entry in value.Value.EnumerateArray())
        {
            var path = $"{ScanRootsKey}[{index++}]";
            if (ResolveRoot(entry, path, homeDirectory, directoryExists, problems) is { } root && !roots.Contains(root))
            {
                roots.Add(root);
            }
        }

        return roots.Count == 0 && index > 0 ? null : roots.ToImmutable();
    }

    /// <summary>The path with a leading <c>~</c> replaced by the home folder; other text stays as it is.</summary>
    /// <param name="text">A folder path as written in the file.</param>
    /// <param name="homeDirectory">What <c>~</c> stands for.</param>
    public static string ExpandHome(string text, string homeDirectory)
    {
        if (text[0] != HomeMarker)
        {
            return text;
        }

        if (text.Length == 1)
        {
            return homeDirectory;
        }

        var separator = text[1];
        if (separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar)
        {
            return Path.Combine(homeDirectory, text[2..]);
        }

        return text;
    }

    private static string? ResolveRoot(
        JsonElement entry,
        string path,
        string homeDirectory,
        Func<string, bool> directoryExists,
        ICollection<ConfigProblem> problems)
    {
        var text = entry.ValueKind == JsonValueKind.String ? entry.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(text))
        {
            problems.Add(new ConfigProblem(path, "a scan root must be a folder path, skipped"));
            return null;
        }

        var expanded = Path.TrimEndingDirectorySeparator(ExpandHome(text, homeDirectory));
        if (!Path.IsPathFullyQualified(expanded))
        {
            var message = $"\"{text}\" is not an absolute path (start it with ~ or /), skipped";
            problems.Add(new ConfigProblem(path, message));
            return null;
        }

        if (!directoryExists(expanded))
        {
            problems.Add(new ConfigProblem(path, $"\"{text}\" is not an existing folder, skipped"));
            return null;
        }

        return expanded;
    }
}
