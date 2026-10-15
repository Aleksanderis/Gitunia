using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Input.Drag;

/// <summary>
/// The mouse behaviour of a pane edge or a title row: a click is reported with its column, a drag grabs the mouse (so it
/// follows the pointer over the other panes) and reports the pointer's screen position. A drag never also counts as a click.
/// </summary>
public sealed class EdgeDragHandler
{
    private readonly DragTracker _tracker = new();

    /// <summary>Raised with the view-relative column of a click.</summary>
    public event Action<int>? Clicked;

    /// <summary>Raised with the screen column and row of the pointer while the left button is held after a drag started.</summary>
    public event Action<int, int>? Dragged;

    /// <summary>True when something listens, so a view without listeners can leave the event to the toolkit.</summary>
    public bool IsListening => Clicked is not null || Dragged is not null;

    /// <summary>Handles a mouse event of <paramref name="view"/>; returns true when it was consumed.</summary>
    public bool Handle(View view, Mouse mouse)
    {
        var screen = mouse.ScreenPosition;
        var phase = _tracker.Feed(mouse.Flags, screen.X, screen.Y);
        DragGrab.Update(view, mouse.Flags, phase);
        switch (phase)
        {
            case DragPhase.Press:
                return true;
            case DragPhase.Start or DragPhase.Move:
                Dragged?.Invoke(screen.X, screen.Y);
                return true;
            case DragPhase.End or DragPhase.SwallowClick:
                return true;
        }

        if (MouseFlagTranslator.IsLeftClick(mouse.Flags) && mouse.Position is { } position)
        {
            Clicked?.Invoke(position.X);
            return true;
        }

        return mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased);
    }
}
