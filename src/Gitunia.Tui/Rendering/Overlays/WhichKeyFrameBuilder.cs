using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Lays out the which-key popup: the keys that can follow the pending prefix, docked above the status bar.</summary>
public static class WhichKeyFrameBuilder
{
    private const int MaximumWidth = 96;
    private const int ChromeRows = 2;
    private const int ScreenMargin = 1;
    private const int KeyGap = 2;

    /// <summary>The popup for <paramref name="pending"/>, or null when there is nothing to show or no room.</summary>
    public static OverlayFrame? Build(string pending, IReadOnlyList<WhichKeyRow> rows, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var capacity = screenHeight - ChromeRows - ScreenMargin - 1;
        if (capacity < 1 || screenWidth < OverlayBox.MinimumWidth)
        {
            return null;
        }

        // A rule between the plain actions and the "+more" groups; it takes a row, so it only shows when both fit.
        var firstGroup = rows.ToList().FindIndex(IsGroup);
        var withRule = firstGroup > 0 && firstGroup < capacity - 1;
        var shown = rows.Take(withRule ? capacity - 1 : capacity).ToList();
        if (shown.Count < 1)
        {
            return null;
        }

        var keysWidth = shown.Max(row => TextWidth.Of(row.Key));
        var descriptionWidth = shown.Max(row => TextWidth.Of(row.Description));
        var wanted = OverlayBox.BorderWidth + 2 + keysWidth + KeyGap + descriptionWidth + 1;
        var width = Math.Clamp(wanted, OverlayBox.MinimumWidth, Math.Min(MaximumWidth, screenWidth));

        var lines = new List<StyledLine> { OverlayBox.Title(KeyFormatter.BreakLigatures($"{pending}-"), width, glyphs) };
        for (var index = 0; index < shown.Count; index++)
        {
            if (withRule && index == firstGroup)
            {
                var rule = new string(glyphs.Horizontal, OverlayBox.InnerWidth(width) - 2);
                lines.Add(OverlayBox.Text(rule, width, glyphs, SegmentRole.Faint));
            }

            var row = shown[index];
            var keys = row.Key + new string(' ', keysWidth - TextWidth.Of(row.Key));
            var builder = new LineBuilder(OverlayBox.InnerWidth(width), glyphs.Ellipsis)
                .Append($" {keys}", SegmentRole.OverlayKey)
                .Append(new string(' ', KeyGap) + row.Description, SegmentRole.Plain);
            lines.Add(OverlayBox.Wrap(builder.Build(), glyphs));
        }

        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame(screenWidth - width, ScreenLayout.StatusRow(screenHeight) - lines.Count, lines);
    }

    private static bool IsGroup(WhichKeyRow row) => row.Description.StartsWith("+more", StringComparison.Ordinal);
}
