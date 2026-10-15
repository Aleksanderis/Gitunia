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
    private bool _isPressed;
    private bool _hasMoved;
    private bool _swallowNextClick;

    /// <summary>Where the button went down.</summary>
    public ScreenPoint Origin { get; private set; }

    /// <summary>True between a press and its release.</summary>
    public bool IsPressed => _isPressed;

    public DragPhase Feed(MouseFlags flags, int x, int y)
    {
        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && flags.HasFlag(MouseFlags.PositionReport))
        {
            return FeedMove(x, y);
        }

        if (flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            _isPressed = true;
            _hasMoved = false;
            _swallowNextClick = false;
            Origin = new ScreenPoint(x, y);
            return DragPhase.Press;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonReleased) && _isPressed)
        {
            _isPressed = false;
            _swallowNextClick = _hasMoved;
            return _hasMoved ? DragPhase.End : DragPhase.None;
        }

        if (MouseFlagTranslator.IsLeftClick(flags) && _swallowNextClick)
        {
            _swallowNextClick = false;
            return DragPhase.SwallowClick;
        }

        return DragPhase.None;
    }

    private DragPhase FeedMove(int x, int y)
    {
        if (!_isPressed)
        {
            return DragPhase.None;
        }

        if (!_hasMoved)
        {
            if (new ScreenPoint(x, y) == Origin)
            {
                return DragPhase.None;
            }

            _hasMoved = true;
            return DragPhase.Start;
        }

        return DragPhase.Move;
    }
}
