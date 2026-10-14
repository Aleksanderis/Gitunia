using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Sidebar;

/// <summary>
/// Keeps the sidebar list view in step with the active tab's state: rebuilds the rows after a change and moves the
/// list selection to the state's cursor. A selection the user made with the mouse is sent back as a command (activating the row is the mouse map's job).
/// </summary>
public sealed class SidebarPresenter
{
    private readonly WorkspaceHost host;
    private readonly ListView list;
    private readonly SidebarListSource source;
    private bool isSyncing;

    public SidebarPresenter(WorkspaceHost host, ListView list, SidebarListSource source)
    {
        this.host = host;
        this.list = list;
        this.source = source;
        host.Changed += Sync;
        list.ValueChanged += (_, _) => OnSelected();
    }

    private void Sync()
    {
        isSyncing = true;
        try
        {
            var tab = host.Workspace.ActiveTab;
            source.Update(tab);
            var rowCount = source.Rows.Length;
            list.SelectedItem = rowCount == 0 || tab is null ? null : SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rowCount);
            if (list.SelectedItem is not null)
            {
                list.EnsureSelectedItemVisible();
            }

            list.SetNeedsDraw();
        }
        finally
        {
            isSyncing = false;
        }
    }

    private void OnSelected()
    {
        if (isSyncing || list.SelectedItem is not { } item || host.Workspace.ActiveTabId is not { } tabId)
        {
            return;
        }

        host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(PaneKind.Sidebar)));
        host.Dispatch(new TabCommand(tabId, new SetSidebarCursorCommand(item)));
    }
}
