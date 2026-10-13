using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Overlays;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>A floating box (help, palette, which-key popup) that is moved and sized to an <see cref="OverlayFrame"/> and hidden when there is none.</summary>
public sealed class OverlayView : View
{
    private OverlayFrame? frame;

    public OverlayView()
    {
        CanFocus = false;
        Visible = false;
    }

    /// <summary>Raised with the frame line (0 is the top border) when the overlay is clicked.</summary>
    public event Action<int>? LineClicked;

    /// <summary>Raised for the wheel over the overlay.</summary>
    public event Action<MouseGesture>? GestureRaised;

    /// <summary>Shows <paramref name="newFrame"/>, or hides the view when it is null.</summary>
    public void Show(OverlayFrame? newFrame)
    {
        frame = newFrame;
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
        if (!MouseFlagTranslator.TryTranslate(mouse.Flags, out var gesture))
        {
            return base.OnMouseEvent(mouse);
        }

        if (gesture == MouseGesture.Click && mouse.Position is { } position)
        {
            LineClicked?.Invoke(position.Y);
        }
        else if (gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            GestureRaised?.Invoke(gesture);
        }

        return true;
    }

    protected override bool OnDrawingContent(Terminal.Gui.ViewBase.DrawContext? context)
    {
        if (frame is null)
        {
            return true;
        }

        for (var row = 0; row < frame.Lines.Count; row++)
        {
            LinePainter.Paint(this, frame.Lines[row], 0, row, isFocused: true);
        }

        return true;
    }
}
