using Gitunia.Tui.Rendering;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>A one-column vertical line between two panes; it is highlighted while the pane beside it has focus.</summary>
public sealed class DividerView(Func<StyledSegment> segment) : View
{
    protected override bool OnDrawingContent(DrawContext? context)
    {
        var line = segment();
        for (var row = 0; row < Viewport.Height; row++)
        {
            LinePainter.Paint(this, new StyledLine([line]), 0, row, isFocused: false);
        }

        return true;
    }
}
