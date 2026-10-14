using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>A rectangle of styled lines to draw over the screen, with its position. Plain data, so frames are testable without a terminal.</summary>
/// <param name="Column">Screen column of the left edge.</param>
/// <param name="Row">Screen row of the top edge.</param>
/// <param name="Lines">The rows; all exactly <see cref="Width"/> columns wide.</param>
public sealed record OverlayFrame(int Column, int Row, IReadOnlyList<StyledLine> Lines)
{
    public int Height => Lines.Count;

    public int Width => Lines.Count == 0 ? 0 : TextWidth.Of(Lines[0].Text);
}
