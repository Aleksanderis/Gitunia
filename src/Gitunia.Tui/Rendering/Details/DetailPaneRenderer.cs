using System.Globalization;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>Draws the detail pane's title and rows, cut to the width.</summary>
public static class DetailPaneRenderer
{
    /// <summary>The title row followed by <paramref name="contentHeight"/> rows starting at the pane's top row; the rows are padded (<see cref="PanePadding"/>).</summary>
    public static IReadOnlyList<StyledLine> RenderPane(DetailPaneModel pane, int width, int contentHeight, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(contentHeight + 1) { RenderTitle(pane.Content, pane.IsFocused, width, glyphs) };
        for (var offset = 0; offset < contentHeight; offset++)
        {
            var row = pane.TopRow + offset;
            var textWidth = PanePadding.TextWidth(width);
            lines.Add(OnPanel(PanePadding.Wrap(row >= 0 && row < pane.Content.Rows.Count
                ? RenderRow(pane.Content.Rows[row], pane.IsFocused && row == pane.SelectedRow, textWidth, glyphs)
                : RenderEmpty(textWidth, glyphs))));
        }

        return lines;
    }

    /// <summary>
    /// The title row, which is the top edge of the pane's box: a stretch of line, the tabs, the commit and its file count, and line
    /// again to the end (the line is <see cref="SegmentRole.Rule"/>, drawn by the frame in the colour of the focus).
    /// </summary>
    public static StyledLine RenderTitle(DetailContent content, bool isFocused, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        var strip = DetailTabStrip.Layout(width, content.Tab);
        var column = 0;
        foreach (var button in strip)
        {
            builder.Append(new string(glyphs.Horizontal, button.Start - column), SegmentRole.Rule);
            var isSelected = DetailTabStrip.TabOf(button) == content.Tab;
            builder.Append(ButtonLabel.For(button.Label, glyphs.IsAscii), isSelected ? (isFocused ? SegmentRole.ActiveTab : SegmentRole.InactiveTab) : SegmentRole.RepositoryTab);
            column = button.Start + button.Width;
        }

        if (content.ShortHash.Length > 0)
        {
            builder.Append(new string(glyphs.Horizontal, 1), SegmentRole.Rule);
            builder.Append($" {content.ShortHash} ", SegmentRole.Hash);
            builder.Append(new string(glyphs.Horizontal, 1), SegmentRole.Rule);
            builder.Append($" {FileCountText(content.FileCount)} ", SegmentRole.Faint);
        }

        return builder.BuildRule(glyphs.Horizontal);
    }

    public static StyledLine RenderRow(DetailRow row, bool isSelected, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis);
        foreach (var segment in row.Segments)
        {
            builder.Append(segment.Text, segment.Role, segment.ColorIndex);
        }

        return builder.Build(SegmentRole.Plain, isSelected);
    }

    /// <summary>A row past the last one: blank.</summary>
    public static StyledLine RenderEmpty(int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis).Build();

    /// <summary>The line marked as part of the panel background; the one place both the text frame and the live list view use.</summary>
    public static StyledLine OnPanel(StyledLine line) =>
        line with { Segments = [.. line.Segments.Select(segment => segment with { OnPanel = true })] };

    // ".. files" until the count is known, so the title does not show 0 and then jump to the real number.
    private static string FileCountText(int? count) => count switch
    {
        null => ".. files",
        1 => "1 file",
        { } number => $"{number.ToString(CultureInfo.InvariantCulture)} files",
    };
}
