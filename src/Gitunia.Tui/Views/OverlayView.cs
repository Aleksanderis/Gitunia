using Gitunia.Tui.Rendering.Overlays;
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

    protected override bool OnDrawingContent(Terminal.Gui.ViewBase.DrawContext? context)
    {
        if (frame is null)
        {
            return true;
        }

        for (var row = 0; row < frame.Lines.Count; row++)
        {
            LinePainter.Paint(this, frame.Lines[row], 0, row, isFocused: false);
        }

        return true;
    }
}
