using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>A one-column vertical line between two panes; it is highlighted while the pane beside it has focus and can be dragged to resize them.</summary>
public sealed class DividerView(Func<StyledSegment> segment) : View
{
    private readonly EdgeDragHandler pointer = new();

    /// <summary>Raised with the screen column and row of the pointer while the divider is dragged.</summary>
    public event Action<int, int>? Dragged
    {
        add => pointer.Dragged += value;
        remove => pointer.Dragged -= value;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var line = segment();
        for (var row = 0; row < Viewport.Height; row++)
        {
            LinePainter.Paint(this, new StyledLine([line]), 0, row, isFocused: false);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse) => pointer.IsListening && pointer.Handle(this, mouse) || base.OnMouseEvent(mouse);
}
