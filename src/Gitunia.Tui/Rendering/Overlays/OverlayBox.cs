using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>Draws the frame around an overlay: a title bar, side rules and a bottom rule. Content rows are built for the inner width.</summary>
public static class OverlayBox
{
    /// <summary>Columns the side rules take.</summary>
    public const int BorderWidth = 2;

    /// <summary>The smallest outer width an overlay is drawn at.</summary>
    public const int MinimumWidth = 16;

    public static int InnerWidth(int width) => width - BorderWidth;

    /// <summary>
    /// The outer width for a popup whose longest line needs <paramref name="contentWidth"/> columns: at least
    /// <paramref name="floor"/>, at most <paramref name="cap"/>, and never wider than the screen minus <paramref name="margin"/>.
    /// </summary>
    public static int FitWidth(int contentWidth, int floor, int cap, int screenWidth, int margin)
    {
        var available = screenWidth - margin;
        return Math.Min(Math.Min(cap, available), Math.Max(floor, contentWidth + BorderWidth + 2));
    }

    public static StyledLine Title(string text, int width, GraphGlyphSet glyphs) =>
        new LineBuilder(width, glyphs.Ellipsis).Append($" {text}", SegmentRole.OverlayTitle).Build(SegmentRole.OverlayTitle);

    /// <summary>A body row of plain text.</summary>
    public static StyledLine Text(string text, int width, GraphGlyphSet glyphs, SegmentRole role = SegmentRole.Plain) =>
        Wrap(new LineBuilder(InnerWidth(width), glyphs.Ellipsis).Append($" {text}", role).Build(), glyphs);

    /// <summary>A body row with a left part and a right part pushed to the right edge.</summary>
    public static StyledLine TwoColumns(
        string left,
        string right,
        int width,
        GraphGlyphSet glyphs,
        SegmentRole leftRole,
        SegmentRole rightRole,
        bool isSelected = false)
    {
        var inner = InnerWidth(width);
        var rightFitted = TextWidth.Fit(right, Math.Max(0, inner / 2), glyphs.Ellipsis);
        var rightWidth = TextWidth.Of(rightFitted);
        var leftFitted = TextWidth.Fit(left, Math.Max(0, inner - rightWidth - 4), glyphs.Ellipsis);
        var gap = Math.Max(1, inner - 2 - TextWidth.Of(leftFitted) - rightWidth);
        var builder = new LineBuilder(inner, glyphs.Ellipsis)
            .Append($" {leftFitted}", leftRole)
            .Append(new string(' ', gap), leftRole)
            .Append(rightFitted, rightRole)
            .Append(" ", rightRole);
        return Wrap(builder.Build(), glyphs, isSelected);
    }

    public static StyledLine Bottom(int width, GraphGlyphSet glyphs)
    {
        var rule = glyphs.Horizontal;
        var ascii = glyphs.IsAscii;
        var text = (ascii ? '+' : glyphs.FrameLine(LineDirections.Up | LineDirections.Right))
            + new string(rule, Math.Max(0, width - BorderWidth))
            + (ascii ? '+' : glyphs.FrameLine(LineDirections.Up | LineDirections.Left));
        return new LineBuilder(width, glyphs.Ellipsis).Append(text, SegmentRole.FocusedBorder).Build();
    }

    public static StyledLine Wrap(StyledLine inner, GraphGlyphSet glyphs, bool isSelected = false)
    {
        var side = new StyledSegment(glyphs.For(LineDirections.Up | LineDirections.Down).ToString(), SegmentRole.FocusedBorder);
        return new StyledLine([side, .. inner.Segments, side], isSelected);
    }
}
