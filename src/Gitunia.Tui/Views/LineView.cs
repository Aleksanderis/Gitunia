using Gitunia.Tui.Input.Drag;
using Gitunia.Tui.Rendering.Styling;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>
/// A one-row view (header, tab row, status bar, pane titles) whose content is built for the current width on every draw.
/// A title row can report clicks (with the column) and drags (with the pointer's screen position).
/// </summary>
public sealed class LineView(Func<int, StyledLine> build) : View
{
    private readonly EdgeDragHandler _pointer = new();

    /// <summary>Raised with the view-relative column of a click.</summary>
    public event Action<int>? Clicked
    {
        add => _pointer.Clicked += value;
        remove => _pointer.Clicked -= value;
    }

    /// <summary>Raised with the screen column and row of the pointer while it drags the row.</summary>
    public event Action<int, int>? Dragged
    {
        add => _pointer.Dragged += value;
        remove => _pointer.Dragged -= value;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        LinePainter.Paint(this, build(Viewport.Width), 0, 0, isFocused: false);
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse) =>
        _pointer.IsListening && _pointer.Handle(this, mouse) || base.OnMouseEvent(mouse);
}
