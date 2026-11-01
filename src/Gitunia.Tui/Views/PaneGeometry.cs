using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.Views;

/// <summary>Where the panes are on the screen now: the history layout and the Changes view layout.</summary>
/// <param name="chrome">The screen size.</param>
/// <param name="arrangement">Which history panes are shown.</param>
/// <param name="working">The focus and zoom of the Changes view.</param>
public sealed class PaneGeometry(IPaneChrome chrome, IPaneArrangement arrangement, IWorkingTreeFocus working)
{
    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    /// <summary>The terminal width.</summary>
    public int ScreenWidth => chrome.ScreenSize.Width;

    /// <summary>The terminal height.</summary>
    public int ScreenHeight => chrome.ScreenSize.Height;

    /// <summary>The Changes view pane that has focus.</summary>
    public PaneKind WorkingFocus => working.FocusedPane();

    /// <summary>The keymap view of the Changes view diff box.</summary>
    public string WorkingDiffView => working.DiffView();

    /// <summary>The staging buttons of the Changes view diff box's title row.</summary>
    public WorkingTreeButtons WorkingDiffButtons() => working.DiffButtons();

    /// <summary>The rectangles of the history panes.</summary>
    public PaneRects Rects()
    {
        var current = arrangement.Arrangement();

        return PaneLayout.Compute(
            ScreenWidth,
            ScreenHeight,
            current.IsDiffOpen,
            current.Zoomed,
            current.Sizes,
            current.ZoomBoth);
    }

    /// <summary>The rectangles of the Changes view panes.</summary>
    public WorkingTreeRects WorkingRects() =>
        WorkingTreeLayout.Compute(ScreenWidth, ScreenHeight, working.FocusedPane(), working.IsDiffZoomed());

    /// <summary>The line between the lists and the diff, on the screen's rows of content.</summary>
    public PaneRect WorkingDividerRect()
    {
        var diff = WorkingRects().Diff;
        if (diff.IsEmpty)
        {
            return Empty;
        }

        return new PaneRect(diff.X - 1, ScreenLayout.GraphTop, 1, ScreenLayout.GraphHeight(ScreenHeight));
    }

    /// <summary>The rectangle of a file list of the Changes view.</summary>
    /// <param name="section">The list.</param>
    public PaneRect ListRect(WorkingTreeSection section) =>
        section == WorkingTreeSection.Staged ? WorkingRects().Staged : WorkingRects().Unstaged;
}
