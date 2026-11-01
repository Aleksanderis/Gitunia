using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Integration;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Reset;

/// <summary>
/// The terminal side of the reset (ux-workflows.md, workflow 3, "Reset"): it turns the reset key into a
/// <see cref="ResetRequested"/> for the active tab, carrying the commit the user sees selected. It decides nothing; the
/// reducer opens the mode prompt, refuses or asks, and the runner performs the effects. The reflog view starts the same
/// flow with <see cref="Begin"/>.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the reset acts on: a sidebar row or the graph commit.</param>
public sealed class ResetActions(Dispatcher host, IBranchSelectionSource selection)
{
    /// <summary>Runs the action when it is the reset key; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (action != ActionIds.ResetBranch)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is { } tabId)
        {
            var chosen = selection.Current();
            var isResettable = chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch
                or SidebarRowKind.Tag;
            host.Dispatch(new ResetRequested(tabId, isResettable ? chosen.CommitHash : null));
        }

        return true;
    }

    /// <summary>
    /// Starts a reset of the tab's current branch to <paramref name="commitHash"/>. Returns the refusal when no prompt
    /// opened (the reflog view keeps its list then), or an empty string when the mode prompt is open.
    /// </summary>
    public string Begin(TabId tabId, string commitHash)
    {
        var before = host.State.Shell.Status.Latest?.Sequence;
        host.Dispatch(new ResetRequested(tabId, commitHash));
        if (host.State.Shell.Prompt is not null)
        {
            return string.Empty;
        }

        // Only a message this request posted is its refusal, not an older one still on the status bar.
        var latest = host.State.Shell.Status.Latest;

        return latest is not null && latest.Sequence != before ? latest.Text : string.Empty;
    }
}
