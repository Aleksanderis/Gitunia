using Gitunia.App;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Views;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Keeps the toolkit focus and the tab's focused pane in step. The state decides which pane has focus and the toolkit
/// focus follows it; a click that gave a view the focus goes the other way and is recorded in the state, so the keymap uses
/// the right view.
/// </summary>
public sealed class PaneFocusCoordinator
{
    private readonly Dispatcher _host;
    private readonly PaneViews _panes;
    private readonly MainView _view;
    private PaneKind _appliedFocus = PaneKind.Graph;

    /// <param name="host">The state.</param>
    /// <param name="panes">The list widgets.</param>
    /// <param name="view">Tells when panes are shown or hidden.</param>
    public PaneFocusCoordinator(Dispatcher host, PaneViews panes, MainView view)
    {
        _host = host;
        _panes = panes;
        _view = view;
        Watch(panes.Graph, () => PaneKind.Graph);
        Watch(panes.Sidebar, () => PaneKind.Sidebar);
        Watch(panes.Detail, () => PaneKind.Detail);
        Watch(panes.Diff, () => PaneKind.Diff);
        Watch(panes.WorkingUnstaged, () => PaneKind.WorkingUnstaged);
        Watch(panes.WorkingStaged, () => PaneKind.WorkingStaged);
        Watch(panes.WorkingDiff, SelectedWorkingDiffPane);
        Watch(panes.WorkingMessage, () => PaneKind.WorkingMessage);
    }

    /// <summary>The state decides which pane has focus; the toolkit focus follows it.</summary>
    public void ApplyFocus()
    {
        var wanted = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        if (wanted == _appliedFocus)
        {
            return;
        }

        _appliedFocus = wanted;
        var pane = _panes.PaneOf(wanted);
        if (!pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    /// <summary>Applies the focus again even when the state did not change (hiding the graph moved the toolkit focus).</summary>
    public void ReapplyFocus()
    {
        _appliedFocus = (PaneKind)(-1);
        ApplyFocus();
    }

    private void Watch(View pane, Func<PaneKind> kind) => pane.HasFocusChanged += (_, _) => OnPaneFocused(pane, kind());

    // The diff pane of the side the selected file is on (a click on the diff box focuses that one).
    private PaneKind SelectedWorkingDiffPane() =>
        WorkingPanes.DiffOf(_host.Workspace.ActiveTab?.WorkingTree?.Selection?.Section ?? WorkingTreeSection.Unstaged);

    // A click gave a view the focus: record it in the state so the keymap uses the right view.
    private void OnPaneFocused(View pane, PaneKind kind)
    {
        // While panes are shown or hidden the toolkit moves the focus on its own; that is not the user's choice.
        var isWorkingPane = WorkingPanes.IsWorking(kind);
        var isWorkingView = _host.Workspace.ActiveTab?.View.Mode == TabViewMode.WorkingTree;
        var isUserChoice = isWorkingPane == isWorkingView && !_view.IsChangingVisibility;
        if (isUserChoice && pane.HasFocus && _host.Workspace.ActiveTabId is { } tabId
            && _host.Workspace.ActiveTab?.View.FocusedPane != kind)
        {
            _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(kind)));
        }
    }
}
