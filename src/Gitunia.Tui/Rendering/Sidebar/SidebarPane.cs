using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Sidebar;

/// <summary>Lays out the sidebar column of a frame: rows from the top row on, blank padding, the filter line last.</summary>
public static class SidebarPane
{
    /// <summary>Rows available for sidebar entries in a pane of <paramref name="height"/> rows (the last row is the filter line).</summary>
    public static int RowCapacity(int height) => Math.Max(0, height - 1);

    /// <returns>Exactly <paramref name="height"/> lines of <paramref name="width"/> columns.</returns>
    public static IReadOnlyList<StyledLine> Compose(SidebarModel model, int width, int height, GraphGlyphSet glyphs, int reservedColumns = 0)
    {
        var lines = new List<StyledLine>(height);
        var capacity = RowCapacity(height);
        for (var offset = 0; offset < capacity; offset++)
        {
            var index = model.TopRow + offset;
            lines.Add(RenderLine(model, index, width, glyphs, reservedColumns));
        }

        if (height > 0)
        {
            lines.Add(SidebarRowRenderer.FilterLine(model.FilterText, model.IsEditingFilter, width, glyphs, model.FilterCaret));
        }

        return lines;
    }

    private static StyledLine RenderLine(
        SidebarModel model,
        int index,
        int width,
        GraphGlyphSet glyphs,
        int reservedColumns)
    {
        if (index < 0 || index >= model.Rows.Length)
        {
            return new LineBuilder(width, glyphs.Ellipsis).Build();
        }

        var row = SidebarRowRenderer.Render(model.Rows[index], width - reservedColumns, index == model.Cursor, glyphs);
        return RowMenuMarker.WithReserve(row, reservedColumns);
    }
}
