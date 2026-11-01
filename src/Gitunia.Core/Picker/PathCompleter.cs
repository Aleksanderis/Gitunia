namespace Gitunia.Core.Picker;

/// <summary>Tab completion for a typed path. Listing the candidates reads the file system and is the picker listing effect.</summary>
public static class PathCompleter
{
    /// <summary>
    /// The text after Tab: extended to the longest prefix all candidates share (plus a separator when there is a single one);
    /// when that adds nothing, completed to the highlighted candidate. Unchanged when there are no candidates.
    /// </summary>
    public static string Complete(string text, IReadOnlyList<PickerRow> candidates, int highlight)
        => Complete(text, candidates, highlight, OperatingSystem.IsWindows());

    /// <summary>
    /// Like the overload without a platform; <paramref name="windows"/> makes a bare <c>~</c> continue with <c>\</c>.
    /// </summary>
    public static string Complete(string text, IReadOnlyList<PickerRow> candidates, int highlight, bool windows)
    {
        if (candidates.Count == 0)
        {
            return text;
        }

        SplitTyped(text, windows, out var directoryText, out var namePrefix);
        directoryText = RealCaseDirectoryText(directoryText, candidates[0].Path);
        var common = CommonPrefix([.. candidates.Select(candidate => candidate.Name)], StringComparison.OrdinalIgnoreCase);
        if (common.Length > namePrefix.Length && !candidates.All(candidate => candidate.Name.StartsWith(common, StringComparison.Ordinal)))
        {
            // Names that differ in case only (Docs, docs2): keep to the part they share exactly, so the text stays a real name.
            common = CommonPrefix([.. candidates.Select(candidate => candidate.Name)], StringComparison.Ordinal);
        }

        if (common.Length > namePrefix.Length)
        {
            return directoryText + common + (candidates.Count == 1 ? Separator(text, windows) : string.Empty);
        }

        var chosen = candidates[Math.Clamp(highlight, 0, candidates.Count - 1)];
        return directoryText + chosen.Name + Separator(text, windows);
    }

    // Rewrites the trailing named segments of the typed directory part to the real case of the candidate's parent directory.
    private static string RealCaseDirectoryText(string directoryText, string candidatePath)
    {
        var realSegments = (Path.GetDirectoryName(candidatePath) ?? string.Empty).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var result = directoryText;
        var end = directoryText.Length - 1;
        for (var offset = 1; offset <= realSegments.Length && end > 0; offset++)
        {
            var start = directoryText.LastIndexOfAny(['/', '\\'], end - 1) + 1;
            var segment = directoryText[start..end];
            var real = realSegments[^offset];
            if (segment.Length == 0 || segment is "~" or "." or ".." || segment.EndsWith(':') || !string.Equals(segment, real, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            result = $"{result[..start]}{real}{result[end..]}";
            end = start - 1;
        }

        return result;
    }

    public static void SplitTyped(string text, bool windows, out string directoryText, out string namePrefix)
    {
        var lastSeparator = text.LastIndexOfAny(['/', '\\']);
        if (lastSeparator < 0)
        {
            // "~", ".", "..": the directory itself, the user has not typed a separator yet.
            directoryText = text is "~" or "." or ".." ? $"{text}{NativeSeparator(windows)}" : string.Empty;
            namePrefix = string.Empty;
            return;
        }

        directoryText = text[..(lastSeparator + 1)];
        namePrefix = text[(lastSeparator + 1)..];
    }

    // A separator the user already typed wins; otherwise the platform's own.
    private static string Separator(string text, bool windows)
    {
        if (text.Contains('\\'))
        {
            return "\\";
        }

        if (text.Contains('/'))
        {
            return "/";
        }

        return NativeSeparator(windows);
    }

    private static string NativeSeparator(bool windows) => windows ? "\\" : "/";

    private static bool CharactersEqual(char left, char right, StringComparison comparison) =>
        comparison == StringComparison.Ordinal ? left == right : char.ToLowerInvariant(left) == char.ToLowerInvariant(right);

    private static string CommonPrefix(List<string> names, StringComparison comparison)
    {
        var prefix = names[0];
        foreach (var name in names.Skip(1))
        {
            var length = 0;
            while (length < prefix.Length && length < name.Length && CharactersEqual(prefix[length], name[length], comparison))
            {
                length++;
            }

            prefix = prefix[..length];
        }

        return prefix;
    }
}
