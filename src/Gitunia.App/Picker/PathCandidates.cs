using Gitunia.Core.Picker;

namespace Gitunia.App.Picker;

/// <summary>Lists the directories that continue a typed path (the file system half of path completion).</summary>
public static class PathCandidates
{
    private const int MaximumCandidates = 500;

    /// <summary>
    /// The subdirectories that continue the typed path, sorted by name; hidden ones only when the typed name starts with a dot.
    /// Every segment of the typed directory part is resolved against the real directory names, ignoring case (an exact spelling wins).
    /// The typed name matches in any case on every platform: it only selects suggestions, the completed text uses the directory's real case.
    /// Stops with <see cref="OperationCanceledException"/> when cancelled (checked between entries).
    /// </summary>
    public static IReadOnlyList<PickerRow> List(
        string text,
        string homeDirectory,
        string baseDirectory,
        IDirectoryLister files,
        CancellationToken cancellationToken)
    {
        PathCompleter.SplitTyped(text, OperatingSystem.IsWindows(), out var directoryText, out var namePrefix);
        var typedDirectory = PathQuery.Resolve(directoryText.Length == 0 ? "." : directoryText, homeDirectory, baseDirectory);
        var directory = ResolveCaseInsensitive(typedDirectory, homeDirectory, baseDirectory, files, cancellationToken);
        var matches = new List<string>();
        foreach (var path in files.ListDirectories(directory))
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
                PickerTags.Directory | (files.IsRepository(path) ? PickerTags.Repository : PickerTags.None)));
        }

        return rows;
    }

    private static string ResolveCaseInsensitive(
        string fullPath,
        string homeDirectory,
        string baseDirectory,
        IDirectoryLister files,
        CancellationToken cancellationToken)
    {
        // No fast path: a case-insensitive file system (macOS, Windows) lists a wrongly cased directory as typed,
        // so every segment below home / base is checked against the real names.
        var current = Path.GetPathRoot(fullPath) ?? string.Empty;
        foreach (var knownDirectory in new[] { homeDirectory, baseDirectory })
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
            current = Path.Combine(current, FindRealName(files.ListDirectories(current), segment));
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
}
