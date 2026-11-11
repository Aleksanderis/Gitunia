using Gitunia.App;
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
    private readonly Dispatcher _host;
    private readonly ListView _list;
    private readonly SidebarListSource _source;
    private bool _isSyncing;

    public SidebarPresenter(Dispatcher host, ListView list, SidebarListSource source)
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
            var rowsChanged = _source.Update(tab);
            var rowCount = _source.Rows.Length;
            var target = rowCount == 0 || tab is null ? null : (int?)SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rowCount);

            // A command that changes neither the rows nor the cursor (progress, a result for another pane) leaves the list alone.
            if (!rowsChanged && _list.SelectedItem == target)
            {
                return;
            }

            _list.SelectedItem = target;
            if (target is not null)
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
