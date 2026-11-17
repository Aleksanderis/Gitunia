namespace Gitunia.Core.Overlays;

/// <summary>The measures every overlay box shares; the renderer draws with them and the confirm check uses them.</summary>
public static class OverlayMetrics
{
    /// <summary>Columns the side rules take.</summary>
    public const int BorderWidth = 2;

    /// <summary>Columns of padding a text row keeps inside the side rules.</summary>
    public const int TextPadding = 2;

    /// <summary>The smallest outer width an overlay is drawn at.</summary>
    public const int MinimumWidth = 16;

    /// <summary>
    /// The widest gap between a popup and the screen edges that is widened away: a popup that leaves one column or so
    /// on each side would show the app's own frame right beside its border, so it takes the whole width.
    /// </summary>
    public const int SnapGap = 3;

    /// <summary>
    /// <paramref name="width"/>, or <paramref name="screenWidth"/> when the popup would leave at most
    /// <see cref="SnapGap"/> columns beside it (and is wide enough to be drawn at all).
    /// </summary>
    public static int Snap(int width, int screenWidth)
    {
        var gap = screenWidth - width;
        return width >= MinimumWidth && gap > 0 && gap <= SnapGap ? screenWidth : width;
    }

    /// <summary>The outer width of a popup of at most <paramref name="cap"/> columns, kept <paramref name="margin"/> from the screen edges, then snapped.</summary>
    public static int WidthFor(int cap, int screenWidth, int margin) =>
        Snap(Math.Min(cap, screenWidth - margin), screenWidth);

    /// <summary>
    /// The outer width for a popup whose longest line needs <paramref name="contentWidth"/> columns: at least
    /// <paramref name="floor"/>, at most <paramref name="cap"/>, and never wider than the screen minus
    /// <paramref name="margin"/>.
    /// </summary>
    public static int FitWidth(int contentWidth, int floor, int cap, int screenWidth, int margin)
    {
        var available = screenWidth - margin;
        return Math.Min(Math.Min(cap, available), Math.Max(floor, contentWidth + BorderWidth + TextPadding));
    }
}
