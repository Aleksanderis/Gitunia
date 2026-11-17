using System.Globalization;
using Gitunia.Core.Hints;
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
    // The key (a digit), the gap after it and " unavailable".
    private const int UnavailableSuffixWidth = 16;
    private const int ScreenMargin = 2;
    private const string Unavailable = " unavailable";

    // The margin, the digit, the gap after it and the margin at the right edge: what is left is the parent's text, so
    // the text fit cannot cut the mark.
    private const int KeyColumns = 5;

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
            var room = OverlayBox.InnerWidth(width) - KeyColumns - (isUsable ? 0 : Unavailable.Length);
            // The key of a parent is its number: the digit that answers, not a binding of the keymap.
            lines.Add(ActionRow.Answer(
                (index + 1).ToString(CultureInfo.InvariantCulture),
                $"{TextWidth.Fit(parents[index], room, glyphs.Ellipsis)}{(isUsable ? string.Empty : Unavailable)}",
                width,
                glyphs,
                isUsable));
        }

        var targets = new List<FrameTarget>();
        lines.Add(OverlayBox.Hints(HintSet.ForMainline(), targets, lines.Count, width, glyphs));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame(
            (screenWidth - width) / 2,
            Math.Max(0, (screenHeight - lines.Count) / 2),
            lines,
            targets);
    }

    /// <summary>The parent (1-based) whose row is the frame line, null for any other line.</summary>
    public static int? ParentAt(int frameLine, int parentCount) =>
        frameLine >= 1 && frameLine <= parentCount ? frameLine : null;
}
