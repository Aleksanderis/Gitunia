namespace Gitunia.Core.Picker;

/// <summary>Directory candidates and Tab completion for a typed path.</summary>
public static class PathCompleter
{
    private const int MaximumCandidates = 500;

    /// <summary>
    /// The subdirectories that continue the typed path, sorted by name; hidden ones only when the typed name starts with a dot.
    /// Every segment of the typed directory part is resolved against the real directory names, ignoring case (an exact spelling wins).
    /// The typed name matches in any case on every platform: it only selects suggestions, the completed text uses the directory's real case.
    /// </summary>
    public static IReadOnlyList<PickerRow> Candidates(string text, PickerContext context) => ListCandidates(text, context, CancellationToken.None);

    /// <summary>Like <see cref="Candidates"/>, but stops with <see cref="OperationCanceledException"/> when cancelled (checked between entries).</summary>
    public static IReadOnlyList<PickerRow> ListCandidates(string text, PickerContext context, CancellationToken cancellationToken)
    {
        SplitTyped(text, OperatingSystem.IsWindows(), out var directoryText, out var namePrefix);
        var typedDirectory = PathQuery.Resolve(directoryText.Length == 0 ? "." : directoryText, context.HomeDirectory, context.BaseDirectory);
        var directory = ResolveCaseInsensitive(typedDirectory, context, cancellationToken);
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

    private static string ResolveCaseInsensitive(string fullPath, PickerContext context, CancellationToken cancellationToken)
    {
        // Fast path: a correctly cased directory lists as is, no ancestor is listed.
        if (context.ListDirectories(fullPath).Count > 0)
        {
            return fullPath;
        }

        var current = Path.GetPathRoot(fullPath) ?? string.Empty;
        foreach (var knownDirectory in new[] { context.HomeDirectory, context.BaseDirectory })
        {
            // Home and base directory are real already, so the walk starts below them.
            var known = Path.TrimEndingDirectorySeparator(knownDirectory);
            if (known.Length > current.Length && fullPath.StartsWith($"{known}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                current = known;
                break;
            }
        }

        var segments = fullPath[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current = Path.Combine(current, FindRealName(context.ListDirectories(current), segment));
        }

        return current;
    }

    private static string FindRealName(IReadOnlyList<string> directories, string segment)
    {
        string? caseOnlyMatch = null;
        foreach (var path in directories)
        {
            var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            if (string.Equals(name, segment, StringComparison.Ordinal))
            {
                return name;
            }

            if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase) && (caseOnlyMatch is null || string.CompareOrdinal(name, caseOnlyMatch) < 0))
            {
                caseOnlyMatch = name;
            }
        }

        return caseOnlyMatch ?? segment;
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

    private static void SplitTyped(string text, bool windows, out string directoryText, out string namePrefix)
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
    private static string Separator(string text, bool windows) =>
        text.Contains('\\') ? "\\" : text.Contains('/') ? "/" : NativeSeparator(windows);

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
