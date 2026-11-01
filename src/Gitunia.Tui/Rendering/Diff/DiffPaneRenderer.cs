using System.Globalization;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Draws the diff pane's title row and rows (a frame without a terminal, for tests and the text frame).</summary>
public static class DiffPaneRenderer
{
    /// <summary>The title row followed by <paramref name="contentHeight"/> rows starting at <see cref="DiffPaneModel.TopRow"/>; the rows are padded (<see cref="PanePadding"/>).</summary>
    public static IReadOnlyList<StyledLine> RenderPane(
        DiffPaneModel pane,
        int width,
        int contentHeight,
        GraphGlyphSet glyphs,
        int paddingColumns = PanePadding.Columns,
        ChipKeys? keys = null)
    {
        var lines = new List<StyledLine>(contentHeight + 1) { RenderTitle(pane, width, glyphs, keys) };
        lines.AddRange(RenderRows(pane, width, contentHeight, glyphs, paddingColumns));
        return lines;
    }

    /// <summary>The rows only, starting at <see cref="DiffPaneModel.TopRow"/>, padded (<see cref="PanePadding"/>); the Changes view draws its own title on the frame.</summary>
    public static IReadOnlyList<StyledLine> RenderRows(DiffPaneModel pane, int width, int contentHeight, GraphGlyphSet glyphs, int paddingColumns = PanePadding.Columns)
    {
        var lines = new List<StyledLine>(contentHeight);
        var gutter = DiffRowRenderer.GutterDigits(pane.Document.MaximumLineNumber);
        for (var offset = 0; offset < contentHeight; offset++)
        {
            var row = pane.TopRow + offset;
            var textWidth = PanePadding.TextWidth(width, paddingColumns);
            lines.Add(PanePadding.Wrap(RenderLine(pane, row, textWidth, gutter, glyphs), paddingColumns));
        }

        return lines;
    }

    /// <summary>The number of hunks that start at or above the cursor row (the <c>n</c> of <c>hunk n/m</c>).</summary>
    public static int HunkPosition(DiffPaneModel pane) =>
        pane.SelectedRow >= 0 && pane.SelectedRow < pane.Document.Rows.Count ? HunksUpTo(pane.Document.HunkRows, pane.SelectedRow) : 0;

    /// <summary>
    /// The title: the file shown, its position among the commit's files (`file n/m`) and the cursor's hunk of it
    /// (`hunk n/m`), then the buttons
    /// (<see cref="DiffTitleButtons"/>) at the right end when the row is wide enough.
    /// </summary>
    public static StyledLine RenderTitle(DiffPaneModel pane, int width, GraphGlyphSet glyphs, ChipKeys? keys = null)
    {
        var buttons = DiffTitleButtons.Layout(width, keys, pane.Document.IsFileContent);
        if (buttons.Count == 0)
        {
            return RenderTitleText(pane, width, glyphs);
        }

        return WithButtons(RenderTitleText(pane, buttons[0].Start - 1, glyphs), buttons, glyphs);
    }

    /// <summary>
    /// The title row of the working-tree diff: <paramref name="title"/> as the pane's chip, then the buttons at the right end
    /// (<see cref="DiffTitleButtons.Layout(int, WorkingTreeButtons, ChipKeys, bool)"/>) when the row is wide enough.
    /// </summary>
    public static StyledLine RenderWorkingTitle(
        string title,
        bool isFocused,
        WorkingTreeButtons staging,
        int width,
        GraphGlyphSet glyphs,
        ChipKeys? keys = null)
    {
        var buttons = DiffTitleButtons.Layout(width, staging, keys);
        var textWidth = buttons.Count > 0 ? buttons[0].Start - 1 : width;
        var text = new LineBuilder(textWidth, glyphs.Ellipsis)
            .Append(glyphs.Horizontal.ToString(), SegmentRole.Rule)
            .Append($" {title} ", isFocused ? SegmentRole.ActiveTab : SegmentRole.SelectedTab)
            .BuildRule(glyphs.Horizontal);

        return buttons.Count == 0 ? text : WithButtons(text, buttons, glyphs);
    }

    private static StyledLine RenderLine(DiffPaneModel pane, int row, int textWidth, int gutter, GraphGlyphSet glyphs)
    {
        if (row < 0 || row >= pane.Document.Rows.Count)
        {
            return new LineBuilder(textWidth, glyphs.Ellipsis).Build();
        }

        return DiffRowRenderer.Render(
            pane.Document.Rows[row],
            textWidth,
            gutter,
            pane.Highlight,
            pane.IsFocused && row == pane.SelectedRow,
            glyphs,
            showWhitespace: pane.ShowWhitespace,
            changes: DiffWordHighlights.ChangesOf(pane.Document, row));
    }

    private static StyledLine WithButtons(StyledLine text, IReadOnlyList<TitleButton> buttons, GraphGlyphSet glyphs)
    {
        var segments = new List<StyledSegment>(text.Segments) { new(glyphs.Horizontal.ToString(), SegmentRole.Rule) };
        foreach (var button in buttons)
        {
            segments.AddRange(KeyedChip.Segments(button, glyphs));
            segments.Add(new StyledSegment(glyphs.Horizontal.ToString(), SegmentRole.Rule));
        }

        segments.Add(new StyledSegment(glyphs.Horizontal.ToString(), SegmentRole.Rule));
        return new StyledLine(segments);
    }

    private static StyledLine RenderTitleText(DiffPaneModel pane, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(glyphs.Horizontal.ToString(), SegmentRole.Rule)
            .Append(
                pane.Document.IsFileContent ? " File " : " Diff ",
                pane.IsFocused ? SegmentRole.ActiveTab : SegmentRole.SelectedTab);
        if (pane.IsRepeating)
        {
            builder.Append($" {RepeatMode.TitleMarker}", SegmentRole.Faint);
        }

        if (pane.Document.ContentPath is { } contentPath)
        {
            if (pane.Document.MaximumLineNumber > 0)
            {
                var count = pane.Document.MaximumLineNumber;
                var unit = count == 1 ? "line" : "lines";
                builder.Append($"  {count.ToString(CultureInfo.InvariantCulture)} {unit}", SegmentRole.Faint);
            }

            AppendWrapMarker(builder, pane);
            if (contentPath.Length > 0)
            {
                builder.Append($"  {contentPath} ", SegmentRole.Plain);
            }
        }
        else if (pane.Document.FileRows.Count > 0
            && pane.SelectedRow >= 0
            && pane.SelectedRow < pane.Document.Rows.Count)
        {
            var fileIndex = Math.Max(0, pane.Document.Rows[pane.SelectedRow].FileIndex);
            var hunksBefore = HunksUpTo(pane.Document.HunkRows, pane.SelectedRow);
            var position = hunksBefore.ToString(CultureInfo.InvariantCulture);
            var total = pane.Document.HunkRows.Count.ToString(CultureInfo.InvariantCulture);
            var fileNumber = pane.FileNumber.ToString(CultureInfo.InvariantCulture);
            var fileCount = pane.FileCount.ToString(CultureInfo.InvariantCulture);
            var files = string.Empty;
            if (pane.IsFolder && pane.FileCount > 0)
            {
                files = $"  folder, {fileCount} {(pane.FileCount == 1 ? "file" : "files")}";
            }
            else if (pane.FileCount > 0)
            {
                files = $"  file {fileNumber}/{fileCount}";
            }

            builder.Append($"{files}  hunk {position}/{total}", SegmentRole.Faint);
            AppendWrapMarker(builder, pane);
            builder.Append($"  {pane.Document.FilePaths[fileIndex][0]} ", SegmentRole.Plain);
        }
        else
        {
            AppendWrapMarker(builder, pane);
        }

        return builder.BuildRule(glyphs.Horizontal);
    }

    // Before the path: when the row is narrow the path is cut, not the marker.
    private static void AppendWrapMarker(LineBuilder builder, DiffPaneModel pane)
    {
        if (pane.IsWrapped)
        {
            builder.Append("  [wrap]", SegmentRole.Faint);
        }

        if (pane.OptionsLabel.Length > 0)
        {
            builder.Append($"  {pane.OptionsLabel}", SegmentRole.Faint);
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
