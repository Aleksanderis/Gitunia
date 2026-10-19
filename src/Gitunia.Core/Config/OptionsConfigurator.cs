using System.Collections.Immutable;
using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>Reads <c>frameStyle</c>, <c>tabs.showBranch</c> and <c>scanRoots</c>; a bad value keeps its default and adds a problem.</summary>
public static class OptionsConfigurator
{
    private const string FrameStyleKey = "frameStyle";
    private const string TabsKey = "tabs";
    private const string ShowBranchKey = "tabs.showBranch";
    private const string ScanRootsKey = "scanRoots";
    private const string ShowBranchProperty = "showBranch";
    private const char HomeMarker = '~';

    /// <summary>Validates the three options.</summary>
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
            ParseFrameStyle(document.FrameStyle, problems),
            ParseShowBranch(document.Tabs, problems),
            ParseScanRoots(document.ScanRoots, homeDirectory, directoryExists, problems));

    /// <summary>The style for a setting value; unknown values give rounded.</summary>
    public static FrameStyle ParseFrameStyle(JsonElement? value, ICollection<ConfigProblem> problems)
    {
        if (value is null or { ValueKind: JsonValueKind.Null })
        {
            return FrameStyle.Rounded;
        }

        var text = value.Value.ValueKind == JsonValueKind.String ? value.Value.GetString() ?? string.Empty : null;
        switch (text?.Trim().ToLowerInvariant())
        {
            case "rounded":
                return FrameStyle.Rounded;
            case "square":
                return FrameStyle.Square;
            default:
                problems.Add(new ConfigProblem(FrameStyleKey, "frameStyle must be \"rounded\" or \"square\", using rounded"));
                return FrameStyle.Rounded;
        }
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
    /// The scan roots: absolute paths or <c>~</c>-relative ones that exist. Entries that cannot be used are reported and
    /// skipped; when none of a non-empty list is usable the conventional folders apply. An empty list turns scanning off.
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
            problems.Add(new ConfigProblem(ScanRootsKey, "scanRoots must be a list of folders, using the default folders"));
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

        var expanded = ExpandHome(text, homeDirectory);
        if (!Path.IsPathRooted(expanded))
        {
            problems.Add(new ConfigProblem(path, $"\"{text}\" is not an absolute path (start it with ~ or /), skipped"));
            return null;
        }

        if (!directoryExists(expanded))
        {
            problems.Add(new ConfigProblem(path, $"\"{text}\" is not an existing folder, skipped"));
            return null;
        }

        return expanded;
    }

    private static string ExpandHome(string text, string homeDirectory)
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
        return separator == Path.DirectorySeparatorChar || separator == Path.AltDirectorySeparatorChar
            ? Path.Combine(homeDirectory, text[2..])
            : text;
    }
}
