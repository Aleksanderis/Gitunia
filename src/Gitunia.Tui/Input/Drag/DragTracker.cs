using Terminal.Gui.Input;

namespace Gitunia.Tui.Input.Drag;

/// <summary>
/// Tells a drag from a click in the stream of Terminal.Gui mouse events: a press, position reports with the button still held
/// and a release. The toolkit also reports a click at the release position after a drag; <see cref="DragPhase.SwallowClick"/>
/// marks it so views do not treat the end of a drag as a click. Pure: the caller feeds flags and a position in one
/// coordinate system (view-relative or screen, as long as it stays the same).
/// </summary>
public sealed class DragTracker
{
    private bool isPressed;
    private bool hasMoved;
    private bool swallowNextClick;

    /// <summary>Where the button went down.</summary>
    public (int X, int Y) Origin { get; private set; }

    /// <summary>True between a press and its release.</summary>
    public bool IsPressed => isPressed;

    public DragPhase Feed(MouseFlags flags, int x, int y)
    {
        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && flags.HasFlag(MouseFlags.PositionReport))
        {
            return FeedMove(x, y);
        }

        if (flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            isPressed = true;
            hasMoved = false;
            swallowNextClick = false;
            Origin = (x, y);
            return DragPhase.Press;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonReleased) && isPressed)
        {
            isPressed = false;
            swallowNextClick = hasMoved;
            return hasMoved ? DragPhase.End : DragPhase.None;
        }

        if ((flags.HasFlag(MouseFlags.LeftButtonClicked) || flags.HasFlag(MouseFlags.LeftButtonDoubleClicked)) && swallowNextClick)
        {
            swallowNextClick = false;
            return DragPhase.SwallowClick;
        }

        return DragPhase.None;
    }

    private DragPhase FeedMove(int x, int y)
    {
        if (!isPressed)
        {
            return DragPhase.None;
        }

        if (!hasMoved)
        {
            if ((x, y) == Origin)
            {
                return DragPhase.None;
            }

            hasMoved = true;
            return DragPhase.Start;
        }

        return DragPhase.Move;
    }
}
