using Gitunia.App;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>The list the quick jump labels: the focused pane's list, null for a pane without a quick jump.</summary>
/// <param name="host">Holds the focused pane.</param>
/// <param name="sessions">The shown tab's lists.</param>
/// <param name="panes">The list widgets.</param>
/// <param name="diffPane">The jump target of the history diff.</param>
/// <param name="workingDiff">The jump target of the Changes view diff.</param>
public sealed class JumpTargets(
    Dispatcher host,
    IActiveSessions sessions,
    PaneViews panes,
    DiffPaneInput diffPane,
    WorkingDiffController workingDiff) : IQuickJumpHost
{
    /// <inheritdoc />
    public IJumpTarget? OpenTarget()
    {
        if (sessions.ActiveSession() is not { } session)
        {
            return null;
        }

        return (host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph) switch
        {
            PaneKind.Graph => new ListJumpTarget(
                PaneKind.Graph,
                session.Source,
                panes.Graph,
                ScreenLayout.ReservedLeadingRows),
            PaneKind.Diff => diffPane.JumpTarget(),
            PaneKind.Detail => new ListJumpTarget(PaneKind.Detail, session.DetailSource, panes.Detail, 0),
            PaneKind.WorkingUnstaged => new ListJumpTarget(PaneKind.WorkingUnstaged, panes.UnstagedSource, panes.WorkingUnstaged, 0),
            PaneKind.WorkingStaged => new ListJumpTarget(PaneKind.WorkingStaged, panes.StagedSource, panes.WorkingStaged, 0),
            PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => workingDiff.JumpTarget(),
            _ => null,
        };
    }

    /// <inheritdoc />
    public void Redraw() => panes.Chrome.RefreshChrome();
}
