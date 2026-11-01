using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// Lays out the Changes view inside the frame (docs/internal/ux-workflows.md, workflow 2): the Unstaged and Staged lists stacked on the left, the
/// diff on top of the message box on the right; the sidebar is hidden, so the view gets the whole width. Screens narrower than
/// <see cref="MinimumWidthForSideBySide"/> columns show one pane at a time: the focused list, or the diff once it has focus. Boxes share their edges
/// (one line between two panes), as in <see cref="PaneLayout"/>.
/// </summary>
public static class WorkingTreeLayout
{
    /// <summary>Narrowest screen that shows the lists beside the diff (docs/internal/ux-workflows.md, workflow 2); narrower ones show one pane at a time.</summary>
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
    /// <param name="isDiffZoomed">The diff has focus and takes the whole view (<c>&lt;space&gt;z</c>, #343); ignored when another pane has focus.</param>
    public static WorkingTreeRects Compute(int screenWidth, int screenHeight, PaneKind focused, bool isDiffZoomed = false)
    {
        var rects = ComputeBoxes(screenWidth, screenHeight, focused);
        if (isDiffZoomed && WorkingPanes.IsDiff(focused) && !rects.Diff.IsEmpty)
        {
            return ZoomedDiff(screenWidth, screenHeight);
        }

        if (focused != PaneKind.WorkingMessage || !rects.Message.IsEmpty)
        {
            return rects;
        }

        // The message box has focus but did not fit beside the diff (a narrow or short screen): it takes the place of the diff, or,
        // on a narrow screen, of the whole view, like the focused list does.
        var none = new PaneRect(0, 0, 0, 0);
        var whole = new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(screenWidth), ScreenLayout.GraphHeight(screenHeight));
        var isNarrow = screenWidth < MinimumWidthForSideBySide;
        var place = isNarrow ? whole : rects.Diff;
        if (place.IsEmpty)
        {
            return rects;
        }

        var unstaged = isNarrow ? none : rects.Unstaged;
        var staged = isNarrow ? none : rects.Staged;
        return new WorkingTreeRects(unstaged, staged, none, place);
    }

    private static WorkingTreeRects ZoomedDiff(int screenWidth, int screenHeight)
    {
        var none = new PaneRect(0, 0, 0, 0);
        var whole = new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(screenWidth), ScreenLayout.GraphHeight(screenHeight));

        return new WorkingTreeRects(none, none, whole, none);
    }

    private static WorkingTreeRects ComputeBoxes(int screenWidth, int screenHeight, PaneKind focused)
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
            if (WorkingPanes.IsDiff(focused))
            {
                return new WorkingTreeRects(none, none, whole, none);
            }

            var unstagedList = onlyStaged ? none : whole;
            var stagedList = onlyStaged ? whole : none;
            return new WorkingTreeRects(unstagedList, stagedList, none, none);
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
            var unstagedSingle = onlyStaged ? none : single;
            var stagedSingle = onlyStaged ? single : none;
            return new WorkingTreeRects(unstagedSingle, stagedSingle, diff, message);
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
