using Gitunia.Core.Lists;
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

    /// <summary>
    /// The rows of the change group the cursor row belongs to, as a range from its first changed row to its last (a note about a
    /// missing newline included), or null when the diff has no group. Cursor on unchanged lines belongs to the group above.
    /// </summary>
    public static RowRange? GroupRowsAt(DiffDocument document, int row)
    {
        var index = HunkIndexAt(document, row);
        if (index < 0)
        {
            return null;
        }

        var first = document.HunkRows[index];
        var last = first;
        while (last + 1 < document.Rows.Count
            && document.Rows[last + 1].Kind is DiffRowKind.Added or DiffRowKind.Removed or DiffRowKind.NoNewline)
        {
            last++;
        }

        return new RowRange(first, last);
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

    /// <summary>How many added or removed rows come before <paramref name="row"/>.</summary>
    public static int ChangedRowsBefore(DiffDocument document, int row)
    {
        var count = 0;
        for (var candidate = 0; candidate < row && candidate < document.Rows.Count; candidate++)
        {
            count += IsChange(document.Rows[candidate]) ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// The row of the added or removed row with zero-based number <paramref name="number"/>, the last one when there are fewer,
    /// or -1 when the diff has no change.
    /// </summary>
    public static int ChangedRowNumbered(DiffDocument document, int number)
    {
        var seen = 0;
        var last = -1;
        for (var row = 0; row < document.Rows.Count; row++)
        {
            if (!IsChange(document.Rows[row]))
            {
                continue;
            }

            if (seen == number)
            {
                return row;
            }

            seen++;
            last = row;
        }

        return last;
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

    private static bool IsChange(DiffRow row) => row.Kind is DiffRowKind.Added or DiffRowKind.Removed;

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
