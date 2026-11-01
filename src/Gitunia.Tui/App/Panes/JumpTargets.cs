using Gitunia.App;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

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
            PaneKind.Graph => GraphTarget(session),
            PaneKind.Diff => diffPane.JumpTarget(),
            PaneKind.Detail => DetailTarget(session),
            PaneKind.WorkingUnstaged => FileListTarget(PaneKind.WorkingUnstaged, panes.UnstagedSource, panes.WorkingUnstaged),
            PaneKind.WorkingStaged => FileListTarget(PaneKind.WorkingStaged, panes.StagedSource, panes.WorkingStaged),
            PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => workingDiff.JumpTarget(),
            _ => null,
        };
    }

    /// <inheritdoc />
    public void Redraw() => panes.Chrome.RefreshChrome();

    private ListJumpTarget GraphTarget(TerminalTabSession session) => new(
        PaneKind.Graph,
        text => session.Source.JumpLabels(panes.Graph, text),
        text =>
        {
            session.Source.JumpText = text;
            panes.Graph.SetNeedsDraw();
        },
        row =>
        {
            panes.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
            panes.Graph.EnsureSelectedItemVisible();
        });

    private ListJumpTarget DetailTarget(TerminalTabSession session) => new(
        PaneKind.Detail,
        text => session.DetailSource.JumpLabels(panes.Detail, text),
        text =>
        {
            session.DetailSource.JumpText = text;
            panes.Detail.SetNeedsDraw();
        },
        row =>
        {
            panes.Detail.SelectedItem = row;
            panes.Detail.EnsureSelectedItemVisible();
        });

    private static ListJumpTarget FileListTarget(PaneKind pane, WorkingTreeListSource source, PaneListView list) => new(
        pane,
        text => source.JumpLabels(list, text),
        text =>
        {
            source.JumpText = text;
            list.SetNeedsDraw();
        },
        row =>
        {
            list.SelectedItem = row;
            list.EnsureSelectedItemVisible();
        });
}
