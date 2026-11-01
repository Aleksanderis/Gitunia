using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Branches;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Branches;

/// <summary>
/// The terminal side of the branch actions: it turns the branch keys into <see cref="BranchCommand"/>s for the active
/// tab. It decides nothing; the reducer does, and the runner performs the effects. The prompts and dialogs it leads to
/// are the shared prompt and confirm model.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class BranchActions(Dispatcher host, IBranchSelectionSource selection) : IActionHandler
{
    /// <summary>Runs the action when it is a branch action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var current = selection.Current();

        // A remote branch (its row, or a graph commit that has only remote ones) is deleted by SyncActions.
        var target = new SyncTarget(current.RowKind, current.Name, current.CommitHash);
        if (action == ActionIds.BranchDelete && RemoteDeleteTarget.IsRemoteOnly(tab.Refs, target))
        {
            return false;
        }

        var command = Request(tab.Id, action, new BranchTarget(current.RowKind, current.Name, current.CommitHash));
        if (command is null)
        {
            return false;
        }

        host.Dispatch(command);

        return true;
    }

    private static BranchCommand? Request(TabId tabId, ActionId action, BranchTarget target)
    {
        if (action == ActionIds.BranchCreate)
        {
            return new BranchCreateRequested(tabId, target, true);
        }

        if (action == ActionIds.BranchCreateOnly)
        {
            return new BranchCreateRequested(tabId, target, false);
        }

        if (action == ActionIds.BranchSwitch)
        {
            return new BranchSwitchRequested(tabId, target);
        }

        if (action == ActionIds.BranchRename)
        {
            return new BranchRenameRequested(tabId, target);
        }

        if (action == ActionIds.BranchDelete)
        {
            return new BranchDeleteRequested(tabId, target);
        }

        if (action == ActionIds.BranchSetUpstream)
        {
            return new BranchSetUpstreamRequested(tabId, target);
        }

        if (action == ActionIds.BranchUnsetUpstream)
        {
            return new BranchUnsetUpstreamRequested(tabId, target);
        }

        if (action == ActionIds.BranchFastForward)
        {
            return new BranchFastForwardRequested(tabId, target);
        }

        return null;
    }
}
