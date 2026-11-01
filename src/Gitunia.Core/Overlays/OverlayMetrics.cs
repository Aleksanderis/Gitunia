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
