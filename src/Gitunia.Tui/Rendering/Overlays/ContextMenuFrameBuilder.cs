using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the actions menu of a row (#471): a small popup hanging under the row, one line per action with its key at
/// the right edge, so the menu teaches the keyboard path. No filter field and no footer: it is not the command palette.
/// </summary>
public static class ContextMenuFrameBuilder
{
    private const string Title = "Actions";
    private const int MaximumWidth = 72;
    private const int ChromeRows = 2;
    private const int ScreenMargin = 1;
    private const int KeyGap = 3;
    private const int Indent = 2;

    /// <summary>The menu frame, or null when the screen is too small to show at least one entry.</summary>
    public static OverlayFrame? Build(
        PaletteState menu,
        MenuAnchor anchor,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs)
    {
        var entries = menu.Entries;
        var width = Math.Min(WidthOf(entries), screenWidth - (2 * ScreenMargin));
        var maximumRows = screenHeight - ChromeRows - ScreenMargin;
        if (entries.Count == 0 || width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        var window = Window(menu, Math.Min(entries.Count, maximumRows));
        var lines = new List<StyledLine> { OverlayBox.Title(Title, width, glyphs) };
        for (var offset = 0; offset < window.RowCount; offset++)
        {
            var index = window.Top + offset;
            var entry = entries[index];
            var role = entry.IsImplemented ? SegmentRole.Plain : SegmentRole.Faint;
            var keyRole = entry.IsImplemented ? SegmentRole.OverlayKey : SegmentRole.Faint;
            var isSelected = index == window.Selected;
            lines.Add(OverlayBox.TwoColumns(entry.Title, entry.Keys, width, glyphs, role, keyRole, isSelected));
        }

        lines.Add(OverlayBox.Bottom(width, glyphs));
        var height = lines.Count;
        var column = Math.Clamp(anchor.Column + Indent, 0, Math.Max(0, screenWidth - width));
        var below = anchor.Row + 1;
        var row = below + height <= screenHeight ? below : Math.Max(0, anchor.Row - height);
        return new OverlayFrame(column, row, lines);
    }

    /// <summary>The index of the entry drawn on <paramref name="frameLine"/>, or null for the border.</summary>
    public static int? EntryIndexAt(PaletteState menu, int frameLine, int screenHeight)
    {
        var rowCount = Math.Min(menu.Entries.Count, screenHeight - ChromeRows - ScreenMargin);
        var offset = frameLine - 1;
        return rowCount >= 1 && offset >= 0 && offset < rowCount ? Window(menu, rowCount).Top + offset : null;
    }

    private static int WidthOf(IReadOnlyList<PaletteEntry> entries)
    {
        var widest = 0;
        foreach (var entry in entries)
        {
            widest = Math.Max(widest, TextWidth.Of(entry.Title) + KeyGap + TextWidth.Of(entry.Keys));
        }

        // Two side rules and a space on each side of the text.
        return Math.Min(MaximumWidth, widest + OverlayBox.BorderWidth + 2);
    }

    private static MenuWindow Window(PaletteState menu, int rowCount)
    {
        var count = menu.Entries.Count;
        var selected = Math.Clamp(menu.SelectedIndex, 0, count - 1);
        var top = Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, count - rowCount));
        return new MenuWindow(rowCount, selected, top);
    }
}
