namespace Gitunia.Core.Picker;

/// <summary>Directory candidates and Tab completion for a typed path.</summary>
public static class PathCompleter
{
    private const int MaximumCandidates = 500;

    /// <summary>
    /// The subdirectories that continue the typed path, sorted by name; hidden ones only when the typed name starts with a dot.
    /// The typed name matches in any case on every platform: it only selects suggestions, the completed text uses the directory's real case.
    /// </summary>
    public static IReadOnlyList<PickerRow> Candidates(string text, PickerContext context) => ListCandidates(text, context, CancellationToken.None);

    /// <summary>Like <see cref="Candidates"/>, but stops with <see cref="OperationCanceledException"/> when cancelled (checked between entries).</summary>
    public static IReadOnlyList<PickerRow> ListCandidates(string text, PickerContext context, CancellationToken cancellationToken)
    {
        SplitTyped(text, out var directoryText, out var namePrefix);
        var directory = PathQuery.Resolve(directoryText.Length == 0 ? "." : directoryText, context.HomeDirectory, context.BaseDirectory);
        var matches = new List<string>();
        foreach (var path in context.ListDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) && (!name.StartsWith('.') || namePrefix.StartsWith('.')))
            {
                matches.Add(path);
            }
        }

        matches.Sort((left, right) => string.Compare(Path.GetFileName(left), Path.GetFileName(right), StringComparison.OrdinalIgnoreCase));

        // Only the rows that are shown cost a file system check.
        var rows = new List<PickerRow>();
        foreach (var path in matches.Take(MaximumCandidates))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new PickerRow(
                path,
                Path.GetFileName(Path.TrimEndingDirectorySeparator(path)),
                PickerTags.Directory | (context.IsRepository(path) ? PickerTags.Repository : PickerTags.None)));
        }

        return rows;
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
        var common = CommonPrefix(candidates.Select(candidate => candidate.Name).ToList(), StringComparison.OrdinalIgnoreCase);
        if (common.Length > namePrefix.Length && !candidates.All(candidate => candidate.Name.StartsWith(common, StringComparison.Ordinal)))
        {
            // Names that differ in case only (Docs, docs2): keep to the part they share exactly, so the text stays a real name.
            common = CommonPrefix(candidates.Select(candidate => candidate.Name).ToList(), StringComparison.Ordinal);
        }

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
