using Gitunia.App;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Worktrees;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Selection;

/// <summary>What a branch, tag or worktree action acts on: the sidebar row under the cursor or the selected commit.</summary>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="panes">The sidebar rows.</param>
/// <param name="graph">The commit selected in the graph.</param>
public sealed class RowSelection(Dispatcher host, PaneViews panes, GraphSelection graph)
    : IBranchSelectionSource, IWorktreeSelectionSource
{
    /// <summary>The sidebar row under the cursor of the active tab, null when there is none.</summary>
    public SidebarRow? CursorRow() => host.Workspace.ActiveTab is { } tab ? SidebarActionReducer.CursorRow(tab) : null;

    /// <summary>A focused sidebar acts on its row, any other pane on the commit selected in the graph.</summary>
    public BranchSelection Current()
    {
        var inSidebar = host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Sidebar;

        return BranchSelection.Of(inSidebar ? CursorRow() : null, graph.SelectedCommit);
    }

    /// <summary>The sidebar row under the cursor (whichever pane has focus) and the commit selected in the graph.</summary>
    WorktreeTarget IWorktreeSelectionSource.Current()
    {
        var row = SelectedSidebarRow();

        return new WorktreeTarget(row?.Kind, row?.Label, row?.Path, row?.CommitHash, graph.SelectedCommit());
    }

    /// <summary>The sidebar row the cursor of the active tab is on, clamped to the rows shown.</summary>
    public SidebarRow? SelectedSidebarRow()
    {
        var rows = panes.Content.SidebarSource.Rows;
        if (host.Workspace.ActiveTab is not { } tab || rows.IsEmpty)
        {
            return null;
        }

        return rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rows.Length)];
    }
}
