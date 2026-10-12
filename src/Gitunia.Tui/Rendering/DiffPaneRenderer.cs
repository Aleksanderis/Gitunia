using System.Globalization;
using Gitunia.Core.Diff;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws the diff pane's title row and rows (a frame without a terminal, for tests and the text frame).</summary>
public static class DiffPaneRenderer
{
    /// <summary>The title row followed by <paramref name="contentHeight"/> rows starting at <paramref name="topRow"/>.</summary>
    public static IReadOnlyList<StyledLine> RenderPane(DiffPaneModel pane, int width, int contentHeight, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(contentHeight + 1) { RenderTitle(pane, width, glyphs) };
        var digits = DiffRowRenderer.GutterDigits(pane.Document.MaximumLineNumber);
        for (var offset = 0; offset < contentHeight; offset++)
        {
            var row = pane.TopRow + offset;
            lines.Add(row >= 0 && row < pane.Document.Rows.Count
                ? DiffRowRenderer.Render(pane.Document.Rows[row], width, digits, pane.Highlight, pane.IsFocused && row == pane.SelectedRow, glyphs)
                : new LineBuilder(width, glyphs.Ellipsis).Build());
        }

        return lines;
    }

    /// <summary>The title: the commit, the file under the cursor and its position among the files and the hunks.</summary>
    public static StyledLine RenderTitle(DiffPaneModel pane, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(" Diff ", pane.IsFocused ? SegmentRole.ActiveTab : SegmentRole.InactiveTab);
        if (pane.ShortHash.Length > 0)
        {
            builder.Append(" " + pane.ShortHash, SegmentRole.Hash);
        }

        var fileCount = pane.Document.FileRows.Count;
        if (fileCount > 0 && pane.SelectedRow >= 0 && pane.SelectedRow < pane.Document.Rows.Count)
        {
            var fileIndex = Math.Max(0, pane.Document.Rows[pane.SelectedRow].FileIndex);
            var hunksBefore = HunksUpTo(pane.Document.HunkRows, pane.SelectedRow);
            builder.Append(
                $"  file {(fileIndex + 1).ToString(CultureInfo.InvariantCulture)}/{fileCount.ToString(CultureInfo.InvariantCulture)}"
                + $"  hunk {hunksBefore.ToString(CultureInfo.InvariantCulture)}/{pane.Document.HunkRows.Count.ToString(CultureInfo.InvariantCulture)}",
                SegmentRole.Faint);
            builder.Append("  " + pane.Document.FilePaths[fileIndex][0], SegmentRole.Plain);
        }

        if (pane.IsWrapped)
        {
            builder.Append("  [wrap]", SegmentRole.Faint);
        }

        return builder.Build(SegmentRole.Plain);
    }

    private static int HunksUpTo(IReadOnlyList<int> hunkRows, int row)
    {
        var low = 0;
        var high = hunkRows.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (hunkRows[middle] <= row)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
