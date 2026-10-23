using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the actions menu of a row (#471): a small popup hanging under the row, one line per action with its key at
/// the right edge, so the menu teaches the keyboard path. No filter field and no footer: it is not the command palette.
/// The same popup is the dropdown of a title row (#645), with a title of its own.
/// </summary>
public static class ContextMenuFrameBuilder
{
    /// <summary>The title of the actions menu of a row.</summary>
    public const string DefaultTitle = "Actions";

    private const int MaximumWidth = 96;
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
        GraphGlyphSet glyphs,
        string title = DefaultTitle)
    {
        var entries = menu.Entries;
        var width = Math.Min(WidthOf(entries), screenWidth - (2 * ScreenMargin));
        var maximumRows = screenHeight - ChromeRows - ScreenMargin;
        if (entries.Count == 0 || width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        var rows = GroupedRows.Of(entries, maximumRows, scrolls: false);
        var selectedEntry = Math.Clamp(menu.SelectedIndex, 0, entries.Count - 1);
        var window = rows.Window(selectedEntry, Math.Min(rows.Lines.Count, maximumRows));
        var lines = new List<StyledLine> { OverlayBox.Title(title, width, glyphs) };
        for (var offset = 0; offset < window.RowCount; offset++)
        {
            var line = rows.Lines[window.Top + offset];
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
            var entry = entries[index];
            var role = entry.IsImplemented ? SegmentRole.Plain : SegmentRole.Faint;
            var keyRole = entry.IsImplemented ? SegmentRole.OverlayKey : SegmentRole.Faint;
            var isSelected = index == selectedEntry;
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
        var maximumRows = screenHeight - ChromeRows - ScreenMargin;
        var rows = GroupedRows.Of(menu.Entries, maximumRows, scrolls: false);
        var rowCount = Math.Min(rows.Lines.Count, maximumRows);
        var offset = frameLine - 1;
        if (rowCount < 1 || offset < 0 || offset >= rowCount)
        {
            return null;
        }

        var window = rows.Window(Math.Clamp(menu.SelectedIndex, 0, menu.Entries.Count - 1), rowCount);
        return rows.EntryAt(window.Top + offset);
    }

    private static int WidthOf(IReadOnlyList<PaletteEntry> entries)
    {
        var widest = 0;
        foreach (var entry in entries)
        {
            widest = Math.Max(widest, TextWidth.Of(entry.Title) + KeyGap + TextWidth.Of(entry.Keys));
            widest = Math.Max(widest, TextWidth.Of(entry.Group ?? string.Empty));
        }

        // Two side rules and a space on each side of the text.
        return Math.Min(MaximumWidth, widest + OverlayBox.BorderWidth + 2);
    }
}
