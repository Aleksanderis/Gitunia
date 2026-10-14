using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Input.Drag;

/// <summary>
/// Keeps the mouse grabbed by the view that got the press while the left button is held, so a drag goes on over other panes.
/// After a drag the toolkit reports a click at the release position to whoever is under the pointer; the grab is therefore kept
/// until that click has reached the dragging view (which swallows it), with a short timeout in case it never comes.
/// </summary>
public static class DragGrab
{
    private static readonly TimeSpan ClickWait = TimeSpan.FromMilliseconds(300);

    // Counts presses: a fallback timeout only lets go of the grab it was scheduled for, not of a newer drag's.
    private static int generation;

    /// <summary>Grabs on a press, lets go on a release or when the click after a drag arrived, and schedules the fallback after a drag.</summary>
    public static void Update(View view, MouseFlags flags, DragPhase phase)
    {
        switch (phase)
        {
            case DragPhase.Press:
                generation++;
                view.App?.Mouse.GrabMouse(view);
                break;
            case DragPhase.End:
                var scheduledFor = generation;
                view.App?.AddTimeout(ClickWait, () =>
                {
                    if (scheduledFor == generation)
                    {
                        Release(view);
                    }

                    return false;
                });
                break;
            case DragPhase.SwallowClick:
                Release(view);
                break;
            default:
                if (flags.HasFlag(MouseFlags.LeftButtonReleased))
                {
                    Release(view);
                }

                break;
        }
    }

    private static void Release(View view)
    {
        if (view.App?.Mouse.IsGrabbed(view) == true)
        {
            view.App.Mouse.UngrabMouse();
        }
    }
}
