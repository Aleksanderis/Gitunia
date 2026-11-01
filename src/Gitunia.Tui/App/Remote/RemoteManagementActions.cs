using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Remotes;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The terminal side of remote management (add, remove, rename, set URL): it turns the keys into
/// <see cref="RemoteCommand"/>s for the active tab. It decides nothing but one refusal that the state does not know
/// yet: a fetch, pull or push of the tab still runs (that state moves with #977). The prompts it leads to are the shared
/// prompt model.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
/// <param name="remoteOperations">Tells whether a fetch, pull or push runs on a tab; remotes stay then.</param>
public sealed class RemoteManagementActions(
    Dispatcher host,
    IBranchSelectionSource selection,
    IRemoteOperationSource remoteOperations)
{
    private const string FetchRunsNotice = "Busy: a fetch, pull or push of this tab is still running";

    /// <summary>What to tell the user when the action was refused.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Runs the action when it is a remote management action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        var kind = KindOf(action);
        if (kind is null)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return true;
        }

        if (remoteOperations.IsRunning(tabId))
        {
            Notice = FetchRunsNotice;
            return true;
        }

        var current = selection.Current();
        host.Dispatch(new RemoteActionRequested(tabId, kind.Value, new RemoteTarget(current.RowKind, current.Name)));

        return true;
    }

    private static RemoteAction? KindOf(ActionId action)
    {
        if (action == ActionIds.RemoteAdd)
        {
            return RemoteAction.Add;
        }

        if (action == ActionIds.RemoteRemove)
        {
            return RemoteAction.Remove;
        }

        if (action == ActionIds.RemoteRename)
        {
            return RemoteAction.Rename;
        }

        return action == ActionIds.RemoteSetUrl ? RemoteAction.SetUrl : null;
    }
}
