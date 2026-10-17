using System.Globalization;
using Gitunia.Core.Reflog;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the reflog list: hash, selector, action and message left, the relative time right.</summary>
public static class ReflogFrameBuilder
{
    private const int MaximumWidth = 110;
    private const int MaximumRows = 20;
    private const int ChromeRows = 3;
    private const int ScreenMargin = 2;
    private const int ShortHashLength = 7;
    private const string Footer = "Esc close";

    /// <summary>The reflog frame for a screen, or null when the screen is too small to show it.</summary>
    public static OverlayFrame? Build(ReflogState state, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var maximumRows = Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows);
        if (width < OverlayBox.MinimumWidth || maximumRows < 1)
        {
            return null;
        }

        var window = Window(state, maximumRows);
        var lines = new List<StyledLine> { OverlayBox.Title($"Reflog: {state.Title}", width, glyphs) };
        if (state.Entries.Count == 0)
        {
            lines.Add(OverlayBox.Text("no reflog entries", width, glyphs, SegmentRole.Faint));
        }

        for (var offset = 0; offset < window.RowCount && window.Top + offset < state.Entries.Count; offset++)
        {
            var entry = state.Entries[window.Top + offset];
            var hash = entry.Hash[..Math.Min(ShortHashLength, entry.Hash.Length)];
            var message = entry.Message.Length == 0 ? entry.Action : $"{entry.Action}: {entry.Message}";
            var selector = $"{state.Title}@{{{entry.Index.ToString(CultureInfo.InvariantCulture)}}}";
            lines.Add(OverlayBox.TwoColumns(
                $"{hash}  {selector}  {message}",
                entry.RelativeDate,
                width,
                glyphs,
                SegmentRole.Plain,
                SegmentRole.Faint,
                window.Top + offset == window.Selected));
        }

        while (lines.Count < window.RowCount + 1)
        {
            lines.Add(OverlayBox.Text(string.Empty, width, glyphs));
        }

        var position = state.Entries.Count == 0 ? 0 : window.Selected + 1;
        lines.Add(OverlayBox.Text(
            string.Create(CultureInfo.InvariantCulture, $"{position}/{state.Entries.Count}  {Footer}"),
            width,
            glyphs,
            SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, 1, lines);
    }

    /// <summary>The entry index of the row on <paramref name="frameLine"/>, or null for none.</summary>
    public static int? EntryIndexAt(ReflogState state, int frameLine, int screenHeight)
    {
        var window = Window(state, Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows));
        var offset = frameLine - 1;
        return offset >= 0 && offset < window.RowCount && window.Top + offset < state.Entries.Count
            ? window.Top + offset
            : null;
    }

    /// <summary>Rows the list shows at a screen height (the page size of the half-page motions).</summary>
    public static int VisibleRows(ReflogState state, int screenHeight) =>
        Window(state, Math.Min(MaximumRows, screenHeight - ScreenMargin - ChromeRows)).RowCount;

    private static PaletteWindow Window(ReflogState state, int maximumRows)
    {
        var count = state.Entries.Count;
        var rowCount = Math.Max(1, Math.Min(maximumRows, count));
        var selected = count == 0 ? 0 : Math.Clamp(state.SelectedIndex, 0, count - 1);
        return new PaletteWindow(
            rowCount,
            selected,
            Math.Clamp(selected - (rowCount / 2), 0, Math.Max(0, count - rowCount)));
    }
}
