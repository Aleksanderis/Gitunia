using Gitunia.Tui.Input.Drag;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>
/// A strip of the frame (an edge row or column, or the line between two panes): it draws the cells of the <see cref="FrameGrid"/>
/// at its place, so the lines, corners and junctions always agree with the text frame. Drawn strips are separate small views
/// (not one view over the screen) so a redraw of the frame never wipes the panes. A line between two panes can be dragged to resize them.
/// </summary>
public sealed class FrameStripView(Func<FrameGrid> grid) : View
{
    private readonly EdgeDragHandler _pointer = new();

    /// <summary>Raised with the screen column and row of the pointer while the strip is dragged.</summary>
    public event Action<int, int>? Dragged
    {
        add => _pointer.Dragged += value;
        remove => _pointer.Dragged -= value;
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var current = grid();
        var origin = Frame;
        for (var row = 0; row < Viewport.Height; row++)
        {
            LinePainter.Paint(this, current.Row(origin.Y + row, origin.X, Viewport.Width), 0, row, isFocused: false);
        }

        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse) => _pointer.IsListening && _pointer.Handle(this, mouse) || base.OnMouseEvent(mouse);
}
