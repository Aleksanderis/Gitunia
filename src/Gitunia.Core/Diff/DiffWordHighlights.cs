namespace Gitunia.Core.Diff;

/// <summary>
/// Finds, for a changed row of a diff, the stretches of its text that differ from the paired row: a run of removed
/// rows is paired line by line with the added rows right after it (the same pairing as the side-by-side view).
/// Pure; looks only at the rows around the one asked for, so drawing the visible rows of a huge diff stays cheap.
/// </summary>
public static class DiffWordHighlights
{
    /// <summary>Longest run of changed rows that is searched for a partner; a longer run gets no word highlight.</summary>
    public const int MaximumRunLength = 2000;

    /// <summary>The changed stretches of the text of <paramref name="row"/>; empty when it has no partner.</summary>
    public static IReadOnlyList<ChangedSpan> ChangesOf(DiffDocument document, int row)
    {
        var rows = document.Rows;
        if (row < 0 || row >= rows.Count || rows[row].Kind is not (DiffRowKind.Removed or DiffRowKind.Added))
        {
            return [];
        }

        var partner = PartnerOf(rows, row);
        if (partner < 0)
        {
            return [];
        }

        var isRemoved = rows[row].Kind == DiffRowKind.Removed;
        var result = isRemoved
            ? WordDiff.Compare(rows[row].Text, rows[partner].Text)
            : WordDiff.Compare(rows[partner].Text, rows[row].Text);

        return isRemoved ? result.Old : result.New;
    }

    /// <summary>The row <paramref name="row"/> is paired with (a removed row with an added one and back), or -1.</summary>
    public static int PartnerOf(IReadOnlyList<DiffRow> rows, int row)
    {
        var kind = rows[row].Kind;
        var start = RunStart(rows, row, kind);
        if (start < 0)
        {
            return -1;
        }

        var offset = row - start;
        if (kind == DiffRowKind.Removed)
        {
            var addedStart = RunEnd(rows, row, kind);

            return addedStart >= 0 && IsRun(rows, addedStart, offset + 1, DiffRowKind.Added)
                ? addedStart + offset
                : -1;
        }

        if (start == 0 || rows[start - 1].Kind != DiffRowKind.Removed)
        {
            return -1;
        }

        var removedStart = RunStart(rows, start - 1, DiffRowKind.Removed);

        return removedStart >= 0 && removedStart + offset < start ? removedStart + offset : -1;
    }

    // First row of the run of rows of one kind that contains row; -1 when the run is longer than the limit.
    private static int RunStart(IReadOnlyList<DiffRow> rows, int row, DiffRowKind kind)
    {
        var start = row;
        while (start > 0 && rows[start - 1].Kind == kind)
        {
            start--;
            if (row - start > MaximumRunLength)
            {
                return -1;
            }
        }

        return start;
    }

    // The row after the run that contains row; -1 when the run is longer than the limit.
    private static int RunEnd(IReadOnlyList<DiffRow> rows, int row, DiffRowKind kind)
    {
        var end = row + 1;
        while (end < rows.Count && rows[end].Kind == kind)
        {
            end++;
            if (end - row > MaximumRunLength)
            {
                return -1;
            }
        }

        return end;
    }

    private static bool IsRun(IReadOnlyList<DiffRow> rows, int start, int count, DiffRowKind kind)
    {
        if (start + count > rows.Count)
        {
            return false;
        }

        for (var index = start; index < start + count; index++)
        {
            if (rows[index].Kind != kind)
            {
                return false;
            }
        }

        return true;
    }
}
