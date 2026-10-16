using Gitunia.Git.Diff.Models;

namespace Gitunia.Core.Diff;

/// <summary>Turns a cursor or a range of rows of a shown diff into the changed lines staging acts on. Pure.</summary>
public static class DiffSelection
{
    /// <summary>
    /// The changed lines of the hunk the cursor row is in (rows before the first hunk belong to it). Empty when the diff has no
    /// hunks (a binary file, a note), which stages the file whole.
    /// </summary>
    public static IReadOnlyList<ChangedLine> OfHunk(DiffDocument document, int row)
    {
        var index = HunkIndexAt(document, row);
        if (index < 0)
        {
            return [];
        }

        var last = index + 1 < document.HunkRows.Count ? document.HunkRows[index + 1] - 1 : document.Rows.Count - 1;
        return OfRows(document, document.HunkRows[index], last);
    }

    /// <summary>The changed lines among rows <paramref name="firstRow"/> to <paramref name="lastRow"/>; context, headers and notes are skipped.</summary>
    public static IReadOnlyList<ChangedLine> OfRows(DiffDocument document, int firstRow, int lastRow)
    {
        var lines = new List<ChangedLine>();
        for (var row = Math.Max(firstRow, 0); row <= lastRow && row < document.Rows.Count; row++)
        {
            var current = document.Rows[row];
            if (current.Kind == DiffRowKind.Added && current.NewLineNumber is { } added)
            {
                lines.Add(new ChangedLine(DiffLineKind.Added, added, current.Text));
            }
            else if (current.Kind == DiffRowKind.Removed && current.OldLineNumber is { } removed)
            {
                lines.Add(new ChangedLine(DiffLineKind.Removed, removed, current.Text));
            }
        }

        return lines;
    }

    /// <summary>The index in <see cref="DiffDocument.HunkRows"/> of the hunk the row is in, or -1 when the diff has none.</summary>
    public static int HunkIndexAt(DiffDocument document, int row)
    {
        var index = -1;
        for (var candidate = 0; candidate < document.HunkRows.Count && document.HunkRows[candidate] <= row; candidate++)
        {
            index = candidate;
        }

        if (index < 0)
        {
            return document.HunkRows.Count > 0 ? 0 : index;
        }

        // Anchors can sit on a change inside a hunk: a header or leading context of the next hunk belongs to its first group.
        return index + 1 < document.HunkRows.Count && HasHeaderBetween(document, document.HunkRows[index], row) ? index + 1 : index;
    }

    private static bool HasHeaderBetween(DiffDocument document, int anchorRow, int row)
    {
        for (var candidate = anchorRow + 1; candidate <= row && candidate < document.Rows.Count; candidate++)
        {
            if (document.Rows[candidate].Kind is DiffRowKind.HunkHeader or DiffRowKind.FileHeader)
            {
                return true;
            }
        }

        return false;
    }
}
