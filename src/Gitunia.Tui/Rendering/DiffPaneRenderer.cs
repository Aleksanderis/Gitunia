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

    /// <summary>
    /// The title: the commit, the file under the cursor and its position among the files and the hunks, then the buttons
    /// (<see cref="DiffTitleButtons"/>) at the right end when the row is wide enough.
    /// </summary>
    public static StyledLine RenderTitle(DiffPaneModel pane, int width, GraphGlyphSet glyphs)
    {
        var buttons = DiffTitleButtons.Layout(width);
        if (buttons.Count == 0)
        {
            return RenderTitleText(pane, width, glyphs);
        }

        var segments = new List<StyledSegment>(RenderTitleText(pane, buttons[0].Start - 1, glyphs).Segments) { new(" ", SegmentRole.Plain) };
        for (var index = 0; index < buttons.Count; index++)
        {
            segments.Add(new StyledSegment(buttons[index].Label, SegmentRole.Button));
            if (index < buttons.Count - 1)
            {
                segments.Add(new StyledSegment(" ", SegmentRole.Plain));
            }
        }

        return new StyledLine(segments);
    }

    private static StyledLine RenderTitleText(DiffPaneModel pane, int width, GraphGlyphSet glyphs)
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
            AppendWrapMarker(builder, pane);
            builder.Append("  " + pane.Document.FilePaths[fileIndex][0], SegmentRole.Plain);
        }
        else
        {
            AppendWrapMarker(builder, pane);
        }

        return builder.Build(SegmentRole.Plain);
    }

    // Before the path: when the row is narrow the path is cut, not the marker.
    private static void AppendWrapMarker(LineBuilder builder, DiffPaneModel pane)
    {
        if (pane.IsWrapped)
        {
            builder.Append("  [wrap]", SegmentRole.Faint);
        }
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
