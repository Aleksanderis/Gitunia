using Gitunia.Tui.Rendering;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>A one-row view (header, tab row, status bar) whose content is built for the current width on every draw.</summary>
public sealed class LineView(Func<int, StyledLine> build) : View
{
    protected override bool OnDrawingContent(Terminal.Gui.ViewBase.DrawContext? context)
    {
        LinePainter.Paint(this, build(Viewport.Width), 0, 0, isFocused: false);
        return true;
    }
}
