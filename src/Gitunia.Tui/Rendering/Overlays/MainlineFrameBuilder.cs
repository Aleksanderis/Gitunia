using System.Globalization;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// Lays out the mainline overlay of a cherry-pick or revert of a merge commit: the question, one row per parent (a click
/// answers it like its digit key) and a hint. Parent n is on frame line n. There is no overlay (null) when it does not
/// fit; the one-key prompt in the status bar still works then.
/// </summary>
public static class MainlineFrameBuilder
{
    private const int MaximumWidth = 72;
    private const int ScreenMargin = 2;
    private const string Hint = "Esc or a click outside cancels";

    /// <summary>The overlay for a screen, or null when it does not fit.</summary>
    public static OverlayFrame? Build(string title, IReadOnlyList<string> parents, int screenWidth, int screenHeight, GraphGlyphSet glyphs)
    {
        var width = Math.Min(MaximumWidth, screenWidth - ScreenMargin);
        var height = parents.Count + 3;
        if (width < OverlayBox.MinimumWidth || height > screenHeight - ScreenMargin)
        {
            return null;
        }

        var lines = new List<StyledLine> { OverlayBox.Title(title, width, glyphs) };
        for (var index = 0; index < parents.Count; index++)
        {
            lines.Add(OverlayBox.TwoColumns(
                parents[index],
                $"[{(index + 1).ToString(CultureInfo.InvariantCulture)}]",
                width,
                glyphs,
                SegmentRole.Plain,
                SegmentRole.OverlayKey));
        }

        lines.Add(OverlayBox.Text(Hint, width, glyphs, SegmentRole.Faint));
        lines.Add(OverlayBox.Bottom(width, glyphs));
        return new OverlayFrame((screenWidth - width) / 2, Math.Max(0, (screenHeight - lines.Count) / 2), lines);
    }

    /// <summary>The parent (1-based) whose row is the frame line, null for any other line.</summary>
    public static int? ParentAt(int frameLine, int parentCount) =>
        frameLine >= 1 && frameLine <= parentCount ? frameLine : null;
}
