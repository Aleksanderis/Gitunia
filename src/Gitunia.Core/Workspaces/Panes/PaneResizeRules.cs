using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Workspaces.Panes;

/// <summary>
/// Pure resize rules of the keys (<c>Ctrl-w &gt;</c> and friends): the new <see cref="PaneSizes"/> for an action on the
/// focused pane. A mouse drag on a divider ends in the same sizes (<c>PaneResizer</c> in the terminal layer).
/// </summary>
public static class PaneResizeRules
{
    /// <summary>Columns a width key moves a divider by (before the count).</summary>
    public const int ColumnStep = 2;

    /// <summary>Rows a height key moves the bottom strip by (before the count).</summary>
    public const int RowStep = 1;

    /// <summary>
    /// The sizes after a resize action on the focused pane, or null when there is nothing to resize (the divider is not shown, or
    /// the size is already at its limit). Growing a pane moves the divider on the side that makes room for it.
    /// </summary>
    /// <param name="current">The sizes now.</param>
    /// <param name="action">One of <c>pane.wider</c>, <c>pane.narrower</c>, <c>pane.taller</c>, <c>pane.shorter</c>, <c>pane.reset-sizes</c>.</param>
    /// <param name="count">The typed count, null for one step.</param>
    /// <param name="focused">The focused pane.</param>
    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="isDiffOpen">The diff pane is open.</param>
    public static PaneSizes? Apply(PaneSizes current, ActionId action, int? count, PaneKind focused, int screenWidth, int screenHeight, bool isDiffOpen)
    {
        if (action == ActionIds.PaneResetSizes)
        {
            return current.IsAutomatic ? null : PaneSizes.Automatic;
        }

        var steps = Math.Max(1, count ?? 1);
        var grows = action == ActionIds.PaneWider || action == ActionIds.PaneTaller;
        PaneSizes? changed;
        if (action == ActionIds.PaneWider || action == ActionIds.PaneNarrower)
        {
            changed = ChangeWidth(current, grows, steps * ColumnStep, focused, screenWidth, isDiffOpen);
        }
        else
        {
            changed = ChangeHeight(current, grows, steps * RowStep, focused, screenHeight);
        }

        return changed is not null && changed != current ? changed : null;
    }

    private static PaneSizes? ChangeWidth(PaneSizes current, bool grows, int amount, PaneKind focused, int screenWidth, bool isDiffOpen)
    {
        var sidebarWidth = PaneSizing.SidebarWidth(screenWidth, current);
        var detailWidth = PaneSizing.DetailWidth(screenWidth, isDiffOpen, current);
        var delta = grows == (focused is PaneKind.Sidebar or PaneKind.Detail) ? amount : -amount;
        if (focused is PaneKind.Sidebar or PaneKind.Graph)
        {
            var resizedSidebar = current with { SidebarWidth = sidebarWidth + delta };
            return sidebarWidth == 0 || PaneSizing.SidebarWidth(screenWidth, resizedSidebar) == sidebarWidth ? null : resizedSidebar;
        }

        if (!isDiffOpen || detailWidth == 0 || detailWidth == screenWidth)
        {
            return null;
        }

        var resized = current with { DetailWidth = detailWidth + delta };

        return PaneSizing.DetailWidth(screenWidth, true, resized) == detailWidth ? null : resized;
    }

    private static PaneSizes? ChangeHeight(PaneSizes current, bool grows, int amount, PaneKind focused, int screenHeight)
    {
        var bottomHeight = PaneSizing.BottomHeight(screenHeight, current);
        if (bottomHeight == 0)
        {
            return null;
        }

        // The bottom strip holds the detail and diff panes; the sidebar and the graph sit above it.
        var growsBottom = grows == (focused is PaneKind.Detail or PaneKind.Diff);
        var resized = current with { BottomHeight = bottomHeight + (growsBottom ? amount : -amount) };

        return PaneSizing.BottomHeight(screenHeight, resized) == bottomHeight ? null : resized;
    }
}
