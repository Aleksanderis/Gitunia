using Gitunia.Tui.Rendering;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>The tab row: draws the tab labels and reports which tab was clicked.</summary>
public sealed class TabBarView(Func<ScreenModel> model, GraphGlyphSet glyphs) : View
{
    /// <summary>Raised with the one-based tab number when a tab is clicked.</summary>
    public event Action<int>? TabClicked;

    protected override bool OnDrawingContent(Terminal.Gui.ViewBase.DrawContext? context)
    {
        LinePainter.Paint(this, ChromeBuilder.Tabs(model(), Viewport.Width, glyphs), 0, 0, isFocused: false);
        return true;
    }

    protected override bool OnMouseEvent(Mouse mouse)
    {
        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked) || mouse.Position is not { } position || model().Tabs is not { Count: > 0 } labels)
        {
            return base.OnMouseEvent(mouse);
        }

        var cells = TabBarLayout.Layout(labels, Viewport.Width, glyphs);
        // The row starts with "< " when tabs are cut off on the left; the layout already counts it in the cell columns.
        if (TabBarLayout.NumberAt(cells, position.X) is { } number)
        {
            TabClicked?.Invoke(number);
        }

        return true;
    }
}
