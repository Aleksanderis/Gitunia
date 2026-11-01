using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Sync;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The terminal side of fetch, pull, push, the push cases that ask first, the delete of a remote branch and the cancel
/// of the running transfer (ux-workflows.md, workflow 5): it turns the keys into <see cref="SyncCommand"/>s for the
/// active tab. It decides nothing; the reducer does, and the runner performs the effects. The prompts and dialogs it
/// leads to are the shared prompt and confirm model.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the remote branch delete acts on.</param>
/// <param name="activity">The operations that run through the shared gate; a pull waits for them.</param>
public sealed class SyncActions(Dispatcher host, IBranchSelectionSource selection, TabActivity activity)
{
    /// <summary>Runs the action when it is one of ours; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (action == ActionIds.Cancel)
        {
            // Esc is only ours while something runs; otherwise it keeps its other meanings.
            return tab.Sync is not null && Send(new SyncCancelRequested(tab.Id));
        }

        if (action == ActionIds.RemoteCancel)
        {
            return Send(new SyncCancelRequested(tab.Id));
        }

        if (KindOf(action) is { } kind)
        {
            return Send(new SyncRequested(tab.Id, kind, activity.IsBusy(tab.Id)));
        }

        if (action != ActionIds.RemoteDeleteBranch && action != ActionIds.BranchDelete)
        {
            return false;
        }

        var current = selection.Current();
        var target = new SyncTarget(current.RowKind, current.Name, current.CommitHash);
        if (action == ActionIds.RemoteDeleteBranch
            || action == ActionIds.BranchDelete && RemoteDeleteTarget.IsRemoteOnly(tab.Refs, target))
        {
            return Send(new SyncDeleteRequested(tab.Id, target));
        }

        return false;
    }

    private static SyncKind? KindOf(ActionId action)
    {
        if (action == ActionIds.RemoteFetch)
        {
            return SyncKind.Fetch;
        }

        if (action == ActionIds.RemoteFetchAll)
        {
            return SyncKind.FetchAll;
        }

        if (action == ActionIds.RemotePull)
        {
            return SyncKind.Pull;
        }

        if (action == ActionIds.RemotePush)
        {
            return SyncKind.Push;
        }

        return action == ActionIds.RemotePushForce ? SyncKind.PushForce : null;
    }

    private bool Send(SyncCommand command)
    {
        host.Dispatch(command);

        return true;
    }
}
