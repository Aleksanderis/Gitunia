using Gitunia.Core.Graph;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Git;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Stash;

/// <summary>
/// Shows a stash's files and diff in the detail pane (#699). The stash is read like a commit keyed by its commit hash,
/// so the pane, the file navigation, the diff options and the caches are the ones commits use; this class decides
/// which stash is shown, follows the stash stack and gives the pane back to the graph selection when the view ends.
/// </summary>
public sealed class StashViewController(
    WorkspaceHost host,
    Func<TabSession?> activeSession,
    Action reselectGraphCommit,
    Action refreshDetail,
    StatusChannel statuses)
{
    /// <summary>Starts showing the stash of a sidebar row.</summary>
    /// <param name="row">A stash row.</param>
    /// <returns>False when the row is no stash of the active tab (the stack changed), so nothing is shown.</returns>
    public bool Show(SidebarRow row)
    {
        if (host.Workspace.ActiveTab is not { } tab
            || activeSession() is not { } session
            || row.Kind != SidebarRowKind.Stash
            || tab.Stashes.FirstOrDefault(candidate => candidate.CommitHash == row.CommitHash) is not { } entry
            || !ObjectId.TryParse(entry.CommitHash, out var commit))
        {
            return false;
        }

        session.StashView.Registry.Add(entry);
        session.StashView.Shown = entry;
        session.Details.Select(commit);
        refreshDetail();

        return true;
    }

    /// <summary>The detail pane shows a stash of the active tab now.</summary>
    public bool IsShowing => activeSession() is { } session && IsShowingIn(session);

    /// <summary>The name of the stash the pane shows in <paramref name="session"/>, null for a commit.</summary>
    public static string? ShownName(TabSession session) => IsShowingIn(session) ? session.StashView.Shown?.Name : null;

    /// <summary>The graph-entry stand-in of the shown stash the detail rows are built for; null for none.</summary>
    public static GraphEntry? ShownEntry(TabSession session)
    {
        if (!IsShowingIn(session) || session.StashView.Shown is not { } stash
            || !ObjectId.TryParse(stash.CommitHash, out var commit))
        {
            return null;
        }

        return new GraphEntry(commit, stash.Message, string.Empty, default, [], new GraphRow(commit, 0, 1, []));
    }

    /// <summary>
    /// The commit that commit actions (branch here, rebase, compare, external tools) act on: the graph selection, but
    /// none while a stash is shown, because the pane then shows the stash, not the graph row (#716).
    /// </summary>
    /// <param name="graphCommit">The hash of the commit selected in the graph, null when none.</param>
    public string? ActionTarget(string? graphCommit) => IsShowing ? null : graphCommit;

    /// <summary>
    /// Whether a graph selection change keeps the shown stash: only the user's own move ends the view, not the binder
    /// restoring the selection after a graph reload, a tab switch or a first load (#716).
    /// </summary>
    /// <param name="binderMovedSelection">The graph tab binder is moving the selection itself.</param>
    public bool KeepsViewOnSelectionChange(bool binderMovedSelection) => binderMovedSelection && IsShowing;

    /// <summary>Ends the stash view and puts the graph selection back in the pane; no-op without one.</summary>
    public void Leave()
    {
        if (activeSession() is not { } session || !IsShowingIn(session))
        {
            return;
        }

        session.StashView.Shown = null;
        reselectGraphCommit();
    }

    /// <summary>
    /// Follows the stash stack after every state change: a shown stash that left the stack ends the view with a warning
    /// (the pane never switches to another stash, since stashes are known by hash); one that only moved in the stack
    /// gets its new name.
    /// </summary>
    public void Reconcile()
    {
        if (host.Workspace.ActiveTab is not { } tab || activeSession() is not { } session
            || session.StashView.Shown is not { } shown)
        {
            return;
        }

        if (!IsShowingIn(session))
        {
            session.StashView.Shown = null;

            return;
        }

        var current = tab.Stashes.FirstOrDefault(candidate => candidate.CommitHash == shown.CommitHash);
        if (current is null)
        {
            session.StashView.Shown = null;

            // A drop or pop from this app already reported the stash by name (with the restore command): keep that.
            if (statuses.Current?.Text.Contains(shown.Name, StringComparison.Ordinal) != true)
            {
                statuses.Warning($"{shown.Name} is no longer in the stash list");
            }

            reselectGraphCommit();
        }
        else if (current != shown)
        {
            session.StashView.Registry.Add(current);
            session.StashView.Shown = current;
            refreshDetail();
        }
    }

    private static bool IsShowingIn(TabSession session) =>
        session.StashView.Shown is { } shown && session.Details.Current.Commit?.ToString() == shown.CommitHash;
}
