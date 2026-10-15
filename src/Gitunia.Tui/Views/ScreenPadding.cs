using Gitunia.Tui.Rendering.Panes;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>Finds the padding of the history and the bottom panes from the width of the screen a view sits in (#244).</summary>
public static class ScreenPadding
{
    /// <summary>The padding columns for <paramref name="view"/>: the width of its outermost view is the screen.</summary>
    public static int Columns(View view)
    {
        var root = view;
        while (root.SuperView is { } parent)
        {
            root = parent;
        }

        return PanePadding.ColumnsFor(root.Viewport.Width);
    }
}
