using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays.Clicks;

/// <summary>
/// A click on a hint, an action row or a list row that the frame on screen lists as clickable (<c>OverlayFrame.Targets</c>):
/// the hint runs its action as if its key had been pressed, a row runs the mouse view the frame names. It is asked first, so
/// every footer is clickable without its overlay knowing about the mouse; a click elsewhere on the frame falls through to the
/// overlays' own hit tests.
/// </summary>
/// <param name="overlay">The view that shows the frame.</param>
public sealed class FrameClickTarget(OverlayView overlay) : IOverlayClickTarget
{
    /// <inheritdoc />
    public OverlayClick? ClickAt(int line, int column)
    {
        if (overlay.Shown?.TargetAt(line, column) is not { } target)
        {
            return null;
        }

        return new OverlayClick(target.View, target.Target, Action: target.Action);
    }
}
