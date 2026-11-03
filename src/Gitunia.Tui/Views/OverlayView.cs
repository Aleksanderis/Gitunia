using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Input.Drag;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>A floating box (help, palette, which-key popup) that is moved and sized to an <see cref="OverlayFrame"/> and hidden when there is none.</summary>
public sealed class OverlayView : View
{
    private readonly DragTracker _drag = new();
    private OverlayFrame? _frame;

    public OverlayView()
    {
        CanFocus = false;
        Visible = false;
    }

    /// <summary>The column inside the frame (0 is the left border) of the last mouse event over the overlay.</summary>
    public int PointerColumn { get; private set; }

    /// <summary>Raised with the frame line (0 is the top border) when the overlay is clicked.</summary>
    public event Action<int>? LineClicked;

    /// <summary>Raised with the frame line when the overlay is double-clicked (the first click was raised as a click).</summary>
    public event Action<int>? LineDoubleClicked;

    /// <summary>Raised with the frame line and column where a drag with the left button began.</summary>
    public event Action<int, int>? DragStarted;

    /// <summary>Raised with the frame line and column of the pointer during a drag (also the first move).</summary>
    public event Action<int, int>? Dragged;

    /// <summary>Raised with the frame line on a triple-click of the overlay.</summary>
    public event Action<int>? LineTripleClicked;

    /// <summary>Raised for the wheel over the overlay.</summary>
    public event Action<MouseGesture>? GestureRaised;

    /// <summary>Shows <paramref name="newFrame"/>, or hides the view when it is null.</summary>
    public void Show(OverlayFrame? newFrame)
    {
        _frame = newFrame;
        if (newFrame is null)
        {
            Visible = false;
            return;
        }

        X = newFrame.Column;
        Y = newFrame.Row;
        Width = newFrame.Width;
        Height = newFrame.Height;
        Visible = true;
        SetNeedsDraw();
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        // A drag never also counts as a click: the click the toolkit reports at its end is swallowed.
        var phase = mouse.Position is { } at ? _drag.Feed(mouse.Flags, at.X, at.Y) : DragPhase.None;
        DragGrab.Update(this, mouse.Flags, phase);
        if (phase != DragPhase.None)
        {
            if (phase == DragPhase.Start)
            {
                DragStarted?.Invoke(_drag.Origin.Y, _drag.Origin.X);
            }

            if (phase is DragPhase.Start or DragPhase.Move && mouse.Position is { } pointerAt)
            {
                Dragged?.Invoke(pointerAt.Y, pointerAt.X);
            }

            return true;
        }

        if (MouseFlagTranslator.TryTranslateSideways(mouse.Flags, out var sideways))
        {
            GestureRaised?.Invoke(sideways);

            return true;
        }

        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        if (mouse.Position is { } pointer)
        {
            PointerColumn = pointer.X;
        }

        if (gesture == MouseGesture.Click && mouse.Position is { } position)
        {
            LineClicked?.Invoke(position.Y);
        }
        else if (gesture == MouseGesture.DoubleClick && mouse.Position is { } doublePosition)
        {
            LineDoubleClicked?.Invoke(doublePosition.Y);
        }
        else if (gesture == MouseGesture.TripleClick && mouse.Position is { } triplePosition)
        {
            LineTripleClicked?.Invoke(triplePosition.Y);
        }
        else if (gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            GestureRaised?.Invoke(gesture);
        }

        return true;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (_frame is null)
        {
            return true;
        }

        for (var row = 0; row < _frame.Lines.Count; row++)
        {
            LinePainter.Paint(this, _frame.Lines[row], 0, row, isFocused: true);
        }

        return true;
    }
}
