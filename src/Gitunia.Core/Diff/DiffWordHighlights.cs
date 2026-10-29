namespace Gitunia.Core.Diff;

/// <summary>
/// Finds, for a changed row of a diff, the stretches of its text that differ from the paired row: a run of removed
/// rows is paired line by line with the added rows right after it (the same pairing as the side-by-side view).
/// Pure; looks only at the rows around the one asked for, so drawing the visible rows of a huge diff stays cheap.
/// </summary>
public static class DiffWordHighlights
{
    /// <summary>Longest run of removed or added rows that is paired; a longer run gets no word highlight.</summary>
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

        if (rows[row].Kind == DiffRowKind.Removed)
        {
            return WordDiff.Compare(rows[row].Text, rows[partner].Text).Old;
        }

        return WordDiff.Compare(rows[partner].Text, rows[row].Text).New;
    }

    /// <summary>The row <paramref name="row"/> is paired with (a removed row with an added one), or -1.</summary>
    public static int PartnerOf(IReadOnlyList<DiffRow> rows, int row)
    {
        var kind = rows[row].Kind;
        if (RunAround(rows, row, kind) is not { } run)
        {
            return -1;
        }

        var offset = row - run.Start;
        if (kind == DiffRowKind.Removed)
        {
            if (run.End >= rows.Count || rows[run.End].Kind != DiffRowKind.Added)
            {
                return -1;
            }

            var added = RunAround(rows, run.End, DiffRowKind.Added);

            return added is { } following && offset < following.End - following.Start ? following.Start + offset : -1;
        }

        if (run.Start == 0 || rows[run.Start - 1].Kind != DiffRowKind.Removed)
        {
            return -1;
        }

        var removed = RunAround(rows, run.Start - 1, DiffRowKind.Removed);

        return removed is { } preceding && offset < preceding.End - preceding.Start ? preceding.Start + offset : -1;
    }

    // The run of rows of one kind that contains row; null when the whole run is longer than the limit, so a long run
    // is left alone from its first row to its last.
    private static RunBounds? RunAround(IReadOnlyList<DiffRow> rows, int row, DiffRowKind kind)
    {
        var start = row;
        while (start > 0 && rows[start - 1].Kind == kind)
        {
            start--;
            if (row - start > MaximumRunLength)
            {
                return null;
            }
        }

        var end = row + 1;
        while (end < rows.Count && rows[end].Kind == kind)
        {
            end++;
            if (end - row > MaximumRunLength)
            {
                return null;
            }
        }

        return end - start > MaximumRunLength ? null : new RunBounds(start, end);
    }

    private readonly record struct RunBounds(int Start, int End);
}
