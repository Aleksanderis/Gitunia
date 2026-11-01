using Gitunia.App;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Keeps the Changes view in step with the active tab's state: shows or hides it, gives the two lists their rows and moves
/// each list's cursor to the state's selection. A row the user clicked is sent back as a command.
/// </summary>
public sealed class WorkingTreePresenter
{
    private readonly Dispatcher _host;
    private readonly PaneViews _panes;
    private readonly MainView _view;
    private readonly IWorkingTreeFocus _focus;
    private PaneKind _lastFocus = PaneKind.WorkingUnstaged;
    private bool _isSyncing;

    /// <param name="host">The state.</param>
    /// <param name="panes">The lists of the Changes view.</param>
    /// <param name="view">Shows and hides the Changes view.</param>
    /// <param name="focus">The focused pane of the Changes view.</param>
    public WorkingTreePresenter(Dispatcher host, PaneViews panes, MainView view, IWorkingTreeFocus focus)
    {
        _host = host;
        _panes = panes;
        _view = view;
        _focus = focus;
        host.Changed += Sync;
        panes.WorkingUnstaged.ValueChanged += (_, _) => OnSelected(panes.WorkingUnstaged, WorkingTreeSection.Unstaged);
        panes.WorkingStaged.ValueChanged += (_, _) => OnSelected(panes.WorkingStaged, WorkingTreeSection.Staged);
    }

    private void Sync()
    {
        var tab = _host.Workspace.ActiveTab;
        var isShown = tab?.View.Mode == TabViewMode.WorkingTree;
        _isSyncing = true;
        try
        {
            var pathStyle = tab?.View.PathStyle ?? FilePathStyle.NameFirst;
            _panes.UnstagedSource.SetPathStyle(pathStyle);
            _panes.StagedSource.SetPathStyle(pathStyle);
            _panes.UnstagedSource.Update(tab?.WorkingTree);
            _panes.StagedSource.Update(tab?.WorkingTree);
            _view.ShowWorkingTree(isShown);
            var selection = tab?.WorkingTree?.Selection;
            Select(_panes.WorkingUnstaged, selection is { Section: WorkingTreeSection.Unstaged } ? selection.Index : null);
            Select(_panes.WorkingStaged, selection is { Section: WorkingTreeSection.Staged } ? selection.Index : null);
        }
        finally
        {
            _isSyncing = false;
        }

        // A narrow screen shows only the focused list or the diff, so the layout depends on the focus.
        var focus = _focus.FocusedPane();
        if (isShown && focus != _lastFocus)
        {
            _panes.Window.SetNeedsLayout();
        }

        _lastFocus = focus;
    }

    private static void Select(ListView list, int? index)
    {
        var count = list.Source?.Count ?? 0;
        list.SelectedItem = index is { } row && count > 0 ? Math.Min(row, count - 1) : null;
        if (list.SelectedItem is not null)
        {
            list.EnsureSelectedItemVisible();
        }

        list.SetNeedsDraw();
    }

    private void OnSelected(ListView list, WorkingTreeSection section)
    {
        if (_isSyncing || list.SelectedItem is not { } item || _host.Workspace.ActiveTabId is not { } tabId)
        {
            return;
        }

        _host.Dispatch(new TabCommand(tabId, new SelectWorkingTreeFileCommand(section, item)));
    }
}
