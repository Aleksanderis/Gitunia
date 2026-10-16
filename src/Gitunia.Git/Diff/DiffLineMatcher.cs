using Gitunia.Git.Diff.Models;

namespace Gitunia.Git.Diff;

/// <summary>Finds lines picked in one diff in another diff of the same file (read with other context lines).</summary>
public static class DiffLineMatcher
{
    /// <summary>
    /// The chosen lines as hunk and line indexes of <paramref name="file"/>, ready for <see cref="DiffPatchWriter.WriteSelection"/>;
    /// null if one of them is not there (a different text at that line number), which means the file changed since.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyCollection<int>>? Match(DiffFile file, IReadOnlyList<ChangedLine> chosen)
    {
        var located = Locate(file);
        var result = new Dictionary<int, List<int>>();
        foreach (var change in chosen)
        {
            if (!located.TryGetValue((change.Kind, change.Number), out var found) || found.Text != change.Text)
            {
                return null;
            }

            if (!result.TryGetValue(found.Hunk, out var indexes))
            {
                indexes = [];
                result[found.Hunk] = indexes;
            }

            indexes.Add(found.Line);
        }

        return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyCollection<int>)pair.Value);
    }

    private static Dictionary<(DiffLineKind Kind, int Number), LocatedLine> Locate(DiffFile file)
    {
        var located = new Dictionary<(DiffLineKind Kind, int Number), LocatedLine>();
        for (var hunkIndex = 0; hunkIndex < file.Hunks.Count; hunkIndex++)
        {
            var lines = file.Hunks[hunkIndex].Lines;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];
                if (line.Kind != DiffLineKind.Context && NumberOf(line) is { } number)
                {
                    located[(line.Kind, number)] = new LocatedLine(hunkIndex, lineIndex, line.Text);
                }
            }
        }

        return located;
    }

    private static int? NumberOf(DiffLine line) => line.Kind == DiffLineKind.Removed ? line.OldLineNumber : line.NewLineNumber;

    private readonly record struct LocatedLine(int Hunk, int Line, string Text);
}
