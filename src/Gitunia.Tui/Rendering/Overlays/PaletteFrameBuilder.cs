using System.Globalization;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the command palette: a query bar, the matching actions with their bindings, a footer.</summary>
public static class PaletteFrameBuilder
{
    private const int MaximumWidth = 72;
    private const int MaximumRows = 12;
    private const int ChromeRows = 3;
    private const int ScreenMargin = 2;
    private const string HintGap = "  ";

    /// <summary>The palette frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(PaletteState palette, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows);
        if (width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        var visible = palette.Visible();
        var window = Window(palette, visible.Count, maximumRows);
        var rowCount = window.RowCount;
        var selected = window.Selected;
        var top = window.Top;

        var lines = new List<StyledLine> { OverlayBox.Title($": {palette.Query}_", width, glyphs) };
        if (visible.Count == 0)
        {
            lines.Add(OverlayBox.Text("no matching action", width, glyphs, SegmentRole.Faint));
        }

        for (var offset = 0; offset < rowCount && top + offset < visible.Count; offset++)
        {
            var entry = visible[top + offset];
            lines.Add(OverlayBox.TwoColumns(entry.Title, entry.Keys, width, glyphs, entry.IsImplemented ? SegmentRole.Plain : SegmentRole.Faint, entry.IsImplemented ? SegmentRole.OverlayKey : SegmentRole.Faint, top + offset == selected));
        }

        while (lines.Count < rowCount + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        lines.Add(Footer(selected, visible.Count, width, glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    private static StyledLine Footer(int selected, int count, int width, GraphGlyphSet glyphs)
    {
        var position = count == 0 ? 0 : selected + 1;
        var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis)
            .Append(string.Create(CultureInfo.InvariantCulture, $" {position}/{count}"), SegmentRole.Status);
        HintRenderer.Append(builder, PaneHints.ForPalette(), HintGap);
        return OverlayBox.Wrap(builder.Build(SegmentRole.Status), glyphs);
    }

    /// <summary>The index into <see cref="PaletteState.Visible"/> of the entry drawn on <paramref name="frameLine"/> of the frame, or null for the title, the footer or an empty row.</summary>
    public static int? EntryIndexAt(PaletteState palette, int frameLine, int screenHeight)
    {
        var visibleCount = palette.Visible().Count;
        var window = Window(palette, visibleCount, Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows));
        var rowCount = window.RowCount;
        var top = window.Top;
        var offset = frameLine - 1;
        return offset >= 0 && offset < rowCount && top + offset < visibleCount ? top + offset : null;
    }

    private static PaletteWindow Window(PaletteState palette, int visibleCount, int maximumRows)
    {
        var rowCount = Math.Max(1, Math.Min(maximumRows, visibleCount));
        var selected = visibleCount == 0 ? 0 : Math.Clamp(palette.SelectedIndex, 0, visibleCount - 1);
        return new PaletteWindow(rowCount, selected, Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, visibleCount - rowCount)));
    }
}
