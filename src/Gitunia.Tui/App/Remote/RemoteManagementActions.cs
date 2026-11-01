using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Remotes;
using Gitunia.Tui.App.Branches;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The terminal side of remote management (add, remove, rename, set URL): it turns the keys into
/// <see cref="RemoteCommand"/>s for the active tab. It decides nothing; the reducer does, also the refusal while a fetch,
/// pull or push of the tab runs. The prompts it leads to are the shared prompt model.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class RemoteManagementActions(Dispatcher host, IBranchSelectionSource selection) : IActionHandler
{
    /// <summary>Runs the action when it is a remote management action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        var kind = KindOf(action);
        if (kind is null)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is not { } tabId)
        {
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
