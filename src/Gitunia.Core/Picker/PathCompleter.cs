namespace Gitunia.Core.Picker;

/// <summary>Directory candidates and Tab completion for a typed path.</summary>
public static class PathCompleter
{
    private const int MaximumCandidates = 500;

    /// <summary>The subdirectories that continue the typed path, sorted by name; hidden ones only when the typed name starts with a dot.</summary>
    public static IReadOnlyList<PickerRow> Candidates(string text, PickerContext context)
    {
        SplitTyped(text, out var directoryText, out var namePrefix);
        var directory = PathQuery.Resolve(directoryText.Length == 0 ? "." : directoryText, context.HomeDirectory, context.BaseDirectory);
        var comparison = namePrefix.Any(char.IsUpper) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var rows = new List<PickerRow>();
        foreach (var path in context.ListDirectories(directory))
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (!name.StartsWith(namePrefix, comparison) || (name.StartsWith('.') && !namePrefix.StartsWith('.')))
            {
                continue;
            }

            var tags = PickerTags.Directory | (context.IsRepository(path) ? PickerTags.Repository : PickerTags.None);
            rows.Add(new PickerRow(path, name, tags));
        }

        rows.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return rows.Count > MaximumCandidates ? rows.GetRange(0, MaximumCandidates) : rows;
    }

    /// <summary>
    /// The text after Tab: extended to the longest prefix all candidates share (plus a separator when there is a single one);
    /// when that adds nothing, completed to the highlighted candidate. Unchanged when there are no candidates.
    /// </summary>
    public static string Complete(string text, IReadOnlyList<PickerRow> candidates, int highlight)
    {
        if (candidates.Count == 0)
        {
            return text;
        }

        SplitTyped(text, out var directoryText, out var namePrefix);
        var common = CommonPrefix(candidates.Select(candidate => candidate.Name).ToList());
        if (common.Length > namePrefix.Length)
        {
            return directoryText + common + (candidates.Count == 1 ? Separator(text) : string.Empty);
        }

        var chosen = candidates[Math.Clamp(highlight, 0, candidates.Count - 1)];
        return directoryText + chosen.Name + Separator(text);
    }

    private static void SplitTyped(string text, out string directoryText, out string namePrefix)
    {
        var lastSeparator = text.LastIndexOfAny(['/', '\\']);
        if (lastSeparator < 0)
        {
            // "~", ".", "..": the directory itself, the user has not typed a separator yet.
            directoryText = text is "~" or "." or ".." ? text + "/" : string.Empty;
            namePrefix = string.Empty;
            return;
        }

        directoryText = text[..(lastSeparator + 1)];
        namePrefix = text[(lastSeparator + 1)..];
    }

    private static string Separator(string text) => text.Contains('\\') ? "\\" : "/";

    private static string CommonPrefix(List<string> names)
    {
        var prefix = names[0];
        foreach (var name in names.Skip(1))
        {
            var length = 0;
            while (length < prefix.Length && length < name.Length && char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(name[length]))
            {
                length++;
            }

            prefix = prefix[..length];
        }

        return prefix;
    }
}
