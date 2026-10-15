using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// Lays out the working-tree view inside the frame (docs/ux.md, workflow 2): the Unstaged and Staged lists stacked on the left, the
/// diff on top of the message box on the right; the sidebar is hidden, so the view gets the whole width. Screens narrower than
/// <see cref="MinimumWidthForSideBySide"/> columns show one pane at a time: the focused list, or the diff once it has focus. Boxes share their edges
/// (one line between two panes), as in <see cref="PaneLayout"/>.
/// </summary>
public static class WorkingTreeLayout
{
    /// <summary>Narrowest screen that shows the lists beside the diff (docs/ux.md, workflow 2); narrower ones show one pane at a time.</summary>
    public const int MinimumWidthForSideBySide = 80;

    /// <summary>Rows of the message box's content: the draft and the option row.</summary>
    public const int MessageHeight = 5;

    /// <summary>Fewest rows of content area at which the two lists are shown stacked; below it only the focused list shows.</summary>
    public const int MinimumStackedHeight = 7;

    /// <summary>Fewest rows of content area at which the message box is shown below the diff.</summary>
    public const int MinimumHeightWithMessage = 12;

    private const int MinimumListWidth = 24;
    private const int MaximumListWidth = 50;
    private const int ListWidthDivisor = 3;
    private const int Divider = 1;

    /// <param name="screenWidth">Terminal columns.</param>
    /// <param name="screenHeight">Terminal rows.</param>
    /// <param name="focused">The focused pane; on narrow screens it decides which list shows.</param>
    public static WorkingTreeRects Compute(int screenWidth, int screenHeight, PaneKind focused)
    {
        var none = new PaneRect(0, 0, 0, 0);
        var inner = ScreenLayout.InnerWidth(screenWidth);
        var area = ScreenLayout.GraphHeight(screenHeight);
        var top = ScreenLayout.GraphTop;
        if (inner <= 0 || area <= 0)
        {
            return new WorkingTreeRects(none, none, none, none);
        }

        var whole = new PaneRect(1, top, inner, area);
        var onlyStaged = WorkingPanes.SectionOf(focused) == WorkingTreeSection.Staged;
        if (screenWidth < MinimumWidthForSideBySide)
        {
            // One pane at a time: the diff once it was asked for (Enter), else the focused list.
            return WorkingPanes.IsDiff(focused)
                ? new WorkingTreeRects(none, none, whole, none)
                : new WorkingTreeRects(onlyStaged ? none : whole, onlyStaged ? whole : none, none, none);
        }

        var listWidth = Math.Clamp(inner / ListWidthDivisor, MinimumListWidth, MaximumListWidth);
        var rightLeft = listWidth + Divider + 1;
        var rightWidth = Math.Max(0, inner - listWidth - Divider);
        var messageHeight = area >= MinimumHeightWithMessage ? MessageHeight : 0;
        var diffHeight = messageHeight > 0 ? area - messageHeight - Divider : area;
        var diff = new PaneRect(rightLeft, top, rightWidth, diffHeight);
        var message = messageHeight > 0 ? new PaneRect(rightLeft, top + diffHeight + Divider, rightWidth, messageHeight) : none;
        if (area < MinimumStackedHeight)
        {
            var single = new PaneRect(1, top, listWidth, area);
            return new WorkingTreeRects(onlyStaged ? none : single, onlyStaged ? single : none, diff, message);
        }

        var unstagedHeight = (area - Divider + 1) / 2;
        var stagedHeight = area - Divider - unstagedHeight;
        return new WorkingTreeRects(
            new PaneRect(1, top, listWidth, unstagedHeight),
            new PaneRect(1, top + unstagedHeight + Divider, listWidth, stagedHeight),
            diff,
            message);
    }
}
