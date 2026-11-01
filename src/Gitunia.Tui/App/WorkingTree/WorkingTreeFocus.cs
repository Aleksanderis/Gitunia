using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>What the layout of the Changes view reads from the active tab: the focused pane, the diff's side and zoom.</summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="sessions">The shown tab's runtime session, which knows the zoom of its diff.</param>
public sealed class WorkingTreeFocus(Dispatcher host, IActiveSessions sessions) : IWorkingTreeFocus
{
    /// <inheritdoc />
    public PaneKind FocusedPane()
    {
        if (host.Workspace.ActiveTab?.View.FocusedPane is { } pane && WorkingPanes.IsWorking(pane))
        {
            return pane;
        }

        return PaneKind.WorkingUnstaged;
    }

    /// <inheritdoc />
    public string DiffView()
    {
        if (host.Workspace.ActiveTab?.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged)
        {
            return DefaultKeymap.WorkingStagedDiffView;
        }

        return DefaultKeymap.WorkingUnstagedDiffView;
    }

    /// <inheritdoc />
    public bool IsDiffZoomed() =>
        sessions.ActiveSession()?.WorkingDiff is { IsZoomed: true }
        && WorkingPanes.IsDiff(host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph);

    /// <inheritdoc />
    public WorkingTreeButtons DiffButtons() =>
        WorkingTreePaneRenderer.StagingButtons(host.Workspace.ActiveTab?.WorkingTree);
}
