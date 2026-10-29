using System.Globalization;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the mainline overlay of a cherry-pick or revert of a merge commit: the question, one row per parent
/// (a click answers it like its digit key; rows git would refuse are dimmed and marked unavailable in the key
/// column, where the text fit cannot cut the mark) and a hint. Parent n is on frame line n. There is no overlay (null)
/// when it does not fit; the one-key prompt in the status bar answers then.
/// </summary>
public static class MainlineFrameBuilder
{
    private const int MaximumWidth = 96;
    private const int MinimumPopupWidth = 72;
    // The index "[n]", " unavailable" and the gap before it.
    private const int UnavailableSuffixWidth = 16;
    private const int ScreenMargin = 2;
    private static readonly HintItem[] Hint = [new("Esc", "or a click outside cancels")];

    /// <summary>The overlay for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(
        string title,
        IReadOnlyList<string> parents,
        int screenWidth,
        int screenHeight,
        GraphGlyphSet glyphs,
        int usableParents)
    {
        var widest = parents.Select(TextWidth.Of).DefaultIfEmpty(0).Max();
        var width = OverlayBox.FitWidth(
            widest + UnavailableSuffixWidth,
            MinimumPopupWidth,
            MaximumWidth,
            screenWidth,
            ScreenMargin);
        var height = parents.Count + 3;
        if (width < OverlayBox.MinimumWidth || height > screenHeight - ScreenMargin)
        {
            return null;
        }

        var lines = new List<StyledLine> { OverlayBox.Title(title, width, glyphs) };
        for (var index = 0; index < parents.Count; index++)
        {
            var isUsable = index < usableParents;
            lines.Add(OverlayBox.TwoColumns(
                parents[index],
                $"[{(index + 1).ToString(CultureInfo.InvariantCulture)}]{(isUsable ? string.Empty : " unavailable")}",
                width,
                glyphs,
                isUsable ? SegmentRole.Plain : SegmentRole.Faint,
                isUsable ? SegmentRole.OverlayKey : SegmentRole.Faint));
        }

        lines.Add(OverlayBox.Hints(Hint, width, glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    /// <summary>The parent (1-based) whose row is the frame line, null for any other line.</summary>
    public static int? ParentAt(int frameLine, int parentCount) =>
        frameLine >= 1 && frameLine <= parentCount ? frameLine : null;
}
