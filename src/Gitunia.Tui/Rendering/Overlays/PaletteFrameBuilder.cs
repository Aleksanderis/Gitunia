using System.Globalization;
using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the command palette: a query bar, the matching actions with their bindings, a footer.</summary>
public static class PaletteFrameBuilder
{
    private const int MaximumWidth = 72;
    private const int MaximumRows = 12;
    private const int ChromeRows = 3;
    private const int ScreenMargin = 2;

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
        var rowCount = Math.Max(1, Math.Min(maximumRows, visible.Count));
        var selected = visible.Count == 0 ? 0 : Math.Clamp(palette.SelectedIndex, 0, visible.Count - 1);
        var top = Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, visible.Count - rowCount));

        var lines = new List<StyledLine> { OverlayBox.Title(": " + palette.Query + "_", width, glyphs) };
        if (visible.Count == 0)
        {
            lines.Add(OverlayBox.Text("no matching action", width, glyphs, SegmentRole.Faint));
        }

        for (var offset = 0; offset < rowCount && top + offset < visible.Count; offset++)
        {
            var entry = visible[top + offset];
            lines.Add(OverlayBox.TwoColumns(entry.Title, entry.Keys, width, glyphs, SegmentRole.Plain, SegmentRole.OverlayKey, top + offset == selected));
        }

        while (lines.Count < rowCount + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        var position = visible.Count == 0 ? 0 : selected + 1;
        lines.Add(OverlayBox.Text(
            string.Create(CultureInfo.InvariantCulture, $"{position}/{visible.Count}  Up/Down choose  Enter run  Esc close"),
            width,
            glyphs,
            SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }
}
