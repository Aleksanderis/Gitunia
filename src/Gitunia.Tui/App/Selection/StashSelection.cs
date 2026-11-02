using Gitunia.App;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Stash;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Selection;

/// <summary>What a stash action acts on: the stash under the sidebar cursor, or the file of the shown stash under the detail cursor.</summary>
/// <param name="host">Holds the active tab and its focused pane.</param>
/// <param name="sessions">The shown tab's session, which holds the shown stash and its rows.</param>
/// <param name="panes">The detail list.</param>
/// <param name="stash">Tells whether a stash is shown.</param>
/// <param name="rows">The sidebar row under the cursor.</param>
public sealed class StashSelection(
    Dispatcher host,
    IActiveSessions sessions,
    PaneViews panes,
    StashViewController stash,
    RowSelection rows) : IStashSelectionSource
{
    /// <inheritdoc />
    public string? CursorStashCommit() => rows.CursorRow() is { Kind: SidebarRowKind.Stash } row ? row.CommitHash : null;

    /// <summary>The file of the shown stash under the cursor of the detail pane's Changes list (restore file).</summary>
    public StashFileTarget? FileUnderCursor()
    {
        if (host.Workspace.ActiveTab is not { View.FocusedPane: PaneKind.Detail }
            || sessions.ActiveSession() is not { } session
            || !stash.IsShowing)
        {
            return null;
        }

        return StashFileTargets.Resolve(
            session.StashView.Shown,
            session.DetailSource.Content,
            panes.Detail.SelectedItem ?? -1,
            session.Details.Current.Details?.UntrackedFileCount ?? 0);
    }
}
