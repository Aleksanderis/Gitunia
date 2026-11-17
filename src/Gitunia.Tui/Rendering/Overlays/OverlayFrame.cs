using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>A rectangle of styled lines to draw over the screen, with its position. Plain data, so frames are testable without a terminal.</summary>
/// <param name="Column">Screen column of the left edge.</param>
/// <param name="Row">Screen row of the top edge.</param>
/// <param name="Lines">The rows; all exactly <see cref="Width"/> columns wide.</param>
/// <param name="Targets">The clickable hints and rows of the frame; empty when none (null is the same as empty).</param>
public sealed record OverlayFrame(
    int Column,
    int Row,
    IReadOnlyList<StyledLine> Lines,
    IReadOnlyList<FrameTarget>? Targets = null)
{
    public int Height => Lines.Count;

    public int Width => Lines.Count == 0 ? 0 : TextWidth.Of(Lines[0].Text);

    /// <summary>The target under a frame position, null for none.</summary>
    /// <param name="line">The frame line (0 is the top border).</param>
    /// <param name="column">The frame column (0 is the left border).</param>
    public FrameTarget? TargetAt(int line, int column) =>
        Targets?.FirstOrDefault(target => target.Contains(line, column));
}
