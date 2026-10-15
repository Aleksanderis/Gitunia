namespace Gitunia.Core.Diff;

/// <summary>
/// Pure arrangement of diff rows for the side-by-side view: unchanged rows appear on both sides, a run of removed rows is paired
/// line by line with the added rows that follow it (the longer side leaves blanks on the other), headers and notes span both sides.
/// </summary>
public static class DiffSplitLayout
{
    private const int NoRow = -1;

    public static DiffSplit Build(DiffDocument document)
    {
        var rows = document.Rows;
        var lines = new List<DiffSplitLine>(rows.Count);
        var lineOfRow = new int[rows.Count];
        var index = 0;
        while (index < rows.Count)
        {
            var kind = rows[index].Kind;
            if (kind is DiffRowKind.Removed or DiffRowKind.Added)
            {
                index = AddChangeRun(rows, index, lines, lineOfRow);
                continue;
            }

            lineOfRow[index] = lines.Count;
            var isContext = kind == DiffRowKind.Context;
            lines.Add(new DiffSplitLine(index, index, !isContext));
            index++;
        }

        return new DiffSplit(lines, lineOfRow);
    }

    // Returns the index after the run: removed rows, then the added rows that follow them.
    private static int AddChangeRun(IReadOnlyList<DiffRow> rows, int start, List<DiffSplitLine> lines, int[] lineOfRow)
    {
        var removedEnd = start;
        while (removedEnd < rows.Count && rows[removedEnd].Kind == DiffRowKind.Removed)
        {
            removedEnd++;
        }

        var addedEnd = removedEnd;
        while (addedEnd < rows.Count && rows[addedEnd].Kind == DiffRowKind.Added)
        {
            addedEnd++;
        }

        var removedCount = removedEnd - start;
        var addedCount = addedEnd - removedEnd;
        for (var offset = 0; offset < Math.Max(removedCount, addedCount); offset++)
        {
            var left = offset < removedCount ? start + offset : NoRow;
            var right = offset < addedCount ? removedEnd + offset : NoRow;
            if (left >= 0)
            {
                lineOfRow[left] = lines.Count;
            }

            if (right >= 0)
            {
                lineOfRow[right] = lines.Count;
            }

            lines.Add(new DiffSplitLine(left, right, false));
        }

        return addedEnd;
    }
}
