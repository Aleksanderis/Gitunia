using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>
/// The mouse side of pane resizing: dragging a divider to a screen position. The keys end in the same
/// <see cref="PaneSizes"/> (<see cref="PaneResizeRules"/>), which <see cref="PaneLayout"/> then clamps to what fits.
/// </summary>
public static class PaneResizer
{
    /// <summary>The sizes after dragging <paramref name="edge"/> so that it sits under the pointer at (<paramref name="column"/>, <paramref name="row"/>).</summary>
    public static PaneSizes Drag(PaneSizes current, PaneEdge edge, int column, int row, int screenHeight) => edge switch
    {
        PaneEdge.Sidebar => current with { SidebarWidth = Math.Max(0, column - 1) },
        PaneEdge.Detail => current with { DetailWidth = Math.Max(0, column - 1) },
        _ => current with { BottomHeight = Math.Max(0, ScreenLayout.GraphTop + ScreenLayout.GraphHeight(screenHeight) - row) },
    };
}
