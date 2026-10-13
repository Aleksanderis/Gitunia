using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the which-key popup: the keys that can follow the pending prefix, docked above the status bar.</summary>
public static class WhichKeyFrameBuilder
{
    private const int MaximumWidth = 56;
    private const int ChromeRows = 2;
    private const int ScreenMargin = 1;
    private const int KeyGap = 2;

    /// <summary>The popup for <paramref name="pending"/>, or null when there is nothing to show or no room.</summary>
    public static OverlayFrame? Build(string pending, IReadOnlyList<WhichKeyRow> rows, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var rowCount = Math.Min(rows.Count, screenHeight - ChromeRows - ScreenMargin - 1);
        if (rowCount < 1 || screenWidth < OverlayBox.MinimumWidth)
        {
            return null;
        }

        var keysWidth = rows.Take(rowCount).Max(row => TextWidth.Of(row.Key));
        var wanted = OverlayBox.BorderWidth + 2 + keysWidth + KeyGap + rows.Take(rowCount).Max(row => TextWidth.Of(row.Description)) + 1;
        var width = Math.Clamp(wanted, OverlayBox.MinimumWidth, Math.Min(MaximumWidth, screenWidth));

        var lines = new List<StyledLine> { OverlayBox.Title(pending + "-", width, glyphs) };
        foreach (var row in rows.Take(rowCount))
        {
            var keys = row.Key + new string(' ', keysWidth - TextWidth.Of(row.Key));
            var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis)
                .Append(" " + keys, SegmentRole.OverlayKey)
                .Append(new string(' ', KeyGap) + row.Description, SegmentRole.Plain);
            lines.Add(OverlayBox.Wrap(builder.Build(), glyphs));
        }

        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame(screenWidth - width, ScreenLayout.StatusRow(screenHeight) - lines.Count, lines);
    }
}
