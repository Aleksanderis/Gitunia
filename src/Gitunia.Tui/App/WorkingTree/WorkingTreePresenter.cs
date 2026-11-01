using Gitunia.App;
using Gitunia.Core.Keymap;
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
    private readonly WorkspaceHost _host;
    private readonly MainView _view;
    private PaneKind _lastFocus = PaneKind.WorkingUnstaged;
    private bool _isSyncing;

    public WorkingTreePresenter(WorkspaceHost host, MainView view)
    {
        _host = host;
        _view = view;
        host.Changed += Sync;
        view.WorkingUnstaged.ValueChanged += (_, _) => OnSelected(view.WorkingUnstaged, WorkingTreeSection.Unstaged);
        view.WorkingStaged.ValueChanged += (_, _) => OnSelected(view.WorkingStaged, WorkingTreeSection.Staged);
        view.WorkingFocus = FocusedPane;
        view.WorkingDiffView = DiffView;
    }

    /// <summary>The pane of the active tab that has focus in the Changes view: a list, a diff or the message box, the Unstaged list when the focus is elsewhere.</summary>
    public PaneKind FocusedPane() =>
        _host.Workspace.ActiveTab?.View.FocusedPane is { } pane && WorkingPanes.IsWorking(pane) ? pane : PaneKind.WorkingUnstaged;

    private void Sync()
    {
        var tab = _host.Workspace.ActiveTab;
        var isShown = tab?.View.Mode == TabViewMode.WorkingTree;
        _isSyncing = true;
        try
        {
            var pathStyle = tab?.View.PathStyle ?? FilePathStyle.NameFirst;
            _view.UnstagedSource.SetPathStyle(pathStyle);
            _view.StagedSource.SetPathStyle(pathStyle);
            _view.UnstagedSource.Update(tab?.WorkingTree);
            _view.StagedSource.Update(tab?.WorkingTree);
            _view.ShowWorkingTree(isShown);
            var selection = tab?.WorkingTree?.Selection;
            Select(_view.WorkingUnstaged, selection is { Section: WorkingTreeSection.Unstaged } ? selection.Index : null);
            Select(_view.WorkingStaged, selection is { Section: WorkingTreeSection.Staged } ? selection.Index : null);
        }
        finally
        {
            _isSyncing = false;
        }

        // A narrow screen shows only the focused list or the diff, so the layout depends on the focus.
        var focus = FocusedPane();
        if (isShown && focus != _lastFocus)
        {
            _view.Window.SetNeedsLayout();
        }

        _lastFocus = focus;
    }

    // The side of the selected file names the diff's view for the mouse map.
    private string DiffView()
    {
        if (_host.Workspace.ActiveTab?.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged)
        {
            return DefaultKeymap.WorkingStagedDiffView;
        }

        return DefaultKeymap.WorkingUnstagedDiffView;
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
