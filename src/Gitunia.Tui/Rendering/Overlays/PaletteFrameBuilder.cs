using System.Globalization;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the command palette: a query bar, the matching actions with their bindings, a footer.</summary>
public static class PaletteFrameBuilder
{
    private const int MaximumWidth = 100;
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
        var rows = GroupedRows.Of(visible, maximumRows, scrolls: true);
        var selectedEntry = visible.Count == 0 ? 0 : Math.Clamp(palette.SelectedIndex, 0, visible.Count - 1);
        var window = rows.Window(selectedEntry, maximumRows);
        var rowCount = window.RowCount;
        var top = window.Top;

        var lines = new List<StyledLine> { OverlayBox.Title($": {palette.Query}_", width, glyphs) };
        if (visible.Count == 0)
        {
            lines.Add(OverlayBox.Text("no matching action", width, glyphs, SegmentRole.Faint));
        }

        for (var offset = 0; offset < rowCount && top + offset < rows.Lines.Count; offset++)
        {
            var line = rows.Lines[top + offset];
            if (line.Header is { } header)
            {
                lines.Add(OverlayBox.Text(header, width, glyphs, SegmentRole.SectionTitle));
                continue;
            }

            if (line.Entry is null)
            {
                lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
                continue;
            }

            var index = line.Entry.Value;
            var entry = visible[index];
            var role = entry.IsImplemented ? SegmentRole.Plain : SegmentRole.Faint;
            var keyRole = entry.IsImplemented ? SegmentRole.OverlayKey : SegmentRole.Faint;
            var isSelected = index == selectedEntry;
            lines.Add(OverlayBox.TwoColumns(entry.Title, entry.Keys, width, glyphs, role, keyRole, isSelected));
        }

        while (lines.Count < rowCount + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        lines.Add(Footer(selectedEntry, visible.Count, width, glyphs));
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

    /// <summary>
    /// The index into <see cref="PaletteState.Visible"/> of the entry drawn on <paramref name="frameLine"/>, or null
    /// for the title, a subheader, the footer or an empty row.
    /// </summary>
    public static int? EntryIndexAt(PaletteState palette, int frameLine, int screenHeight)
    {
        var visible = palette.Visible();
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows);
        var rows = GroupedRows.Of(visible, maximumRows, scrolls: true);
        var selectedEntry = visible.Count == 0 ? 0 : Math.Clamp(palette.SelectedIndex, 0, visible.Count - 1);
        var window = rows.Window(selectedEntry, maximumRows);
        var offset = frameLine - 1;
        return offset >= 0 && offset < window.RowCount ? rows.EntryAt(window.Top + offset) : null;
    }
}
