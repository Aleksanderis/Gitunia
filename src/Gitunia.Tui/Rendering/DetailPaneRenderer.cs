using System.Globalization;

namespace Gitunia.Tui.Rendering;

/// <summary>Draws the detail pane's title and rows, cut to the width.</summary>
public static class DetailPaneRenderer
{
    /// <summary>The title row followed by <paramref name="contentHeight"/> rows starting at the pane's top row.</summary>
    public static IReadOnlyList<StyledLine> RenderPane(DetailPaneModel pane, int width, int contentHeight, GraphGlyphSet glyphs)
    {
        var lines = new List<StyledLine>(contentHeight + 1) { RenderTitle(pane.Content, pane.IsFocused, width, glyphs) };
        for (var offset = 0; offset < contentHeight; offset++)
        {
            var row = pane.TopRow + offset;
            lines.Add(row >= 0 && row < pane.Content.Rows.Count
                ? RenderRow(pane.Content.Rows[row], pane.IsFocused && row == pane.SelectedRow, width, glyphs)
                : RenderEmpty(width, glyphs));
        }

        return lines;
    }

    public static StyledLine RenderTitle(DetailContent content, bool isFocused, int width, GraphGlyphSet glyphs)
    {
        var builder = new LineBuilder(width, glyphs.Ellipsis)
            .Append(" Details ", isFocused ? SegmentRole.ActiveTab : SegmentRole.InactiveTab);
        if (content.ShortHash.Length > 0)
        {
            builder.Append(" " + content.ShortHash, SegmentRole.Hash);
            builder.Append("  " + content.Files.Count.ToString(CultureInfo.InvariantCulture) + (content.Files.Count == 1 ? " file" : " files"), SegmentRole.Faint);
        }

        return builder.Build(SegmentRole.Plain);
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
        new LineBuilder(width, glyphs.Ellipsis).Build(SegmentRole.Plain);
}
