using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Sidebar;

/// <summary>
/// Keeps the sidebar list view in step with the active tab's state: rebuilds the rows after a change and moves the
/// list selection to the state's cursor. A selection the user made with the mouse is sent back as a command (activating the row is the mouse map's job).
/// </summary>
public sealed class SidebarPresenter
{
    private readonly WorkspaceHost _host;
    private readonly ListView _list;
    private readonly SidebarListSource _source;
    private bool _isSyncing;

    public SidebarPresenter(WorkspaceHost host, ListView list, SidebarListSource source)
    {
        _host = host;
        _list = list;
        _source = source;
        host.Changed += Sync;
        list.ValueChanged += (_, _) => OnSelected();
    }

    private void Sync()
    {
        _isSyncing = true;
        try
        {
            var tab = _host.Workspace.ActiveTab;
            _source.Update(tab);
            var rowCount = _source.Rows.Length;
            _list.SelectedItem = rowCount == 0 || tab is null ? null : SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rowCount);
            if (_list.SelectedItem is not null)
            {
                _list.EnsureSelectedItemVisible();
            }

            _list.SetNeedsDraw();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnSelected()
    {
        if (_isSyncing || _list.SelectedItem is not { } item || _host.Workspace.ActiveTabId is not { } tabId)
        {
            return;
        }

        _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Sidebar)));
        _host.Dispatch(new TabCommand(tabId, new SetSidebarCursorCommand(item)));
    }
}
