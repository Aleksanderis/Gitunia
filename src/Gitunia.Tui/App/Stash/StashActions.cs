using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Stash;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Stash;

/// <summary>
/// The terminal side of the stash actions: it turns the stash keys into <see cref="StashCommand"/>s for the active
/// tab, carrying the target the user sees (the stash row or the file). It decides nothing; the reducer does, and the
/// runner performs the effects. The message prompt and the drop dialog are shared shell state.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class StashActions(Dispatcher host, IStashSelectionSource selection)
{
    /// <summary>Runs the action when it is a stash action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (!Owns(action))
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is { } tabId)
        {
            host.Dispatch(Request(tabId, action));
        }

        return true;
    }

    private static bool Owns(ActionId action) =>
        action == ActionIds.StashPush || action == ActionIds.StashPushUntracked || action == ActionIds.StashApply
        || action == ActionIds.StashPop || action == ActionIds.StashDrop || action == ActionIds.StashRestoreFile;

    private StashCommand Request(TabId tabId, ActionId action)
    {
        if (action == ActionIds.StashPush || action == ActionIds.StashPushUntracked)
        {
            return new StashPushRequested(tabId, action == ActionIds.StashPushUntracked);
        }

        if (action == ActionIds.StashRestoreFile)
        {
            return new StashRestoreFileRequested(tabId, selection.FileUnderCursor());
        }

        if (action == ActionIds.StashDrop)
        {
            return new StashDropRequested(tabId, selection.CursorStashCommit());
        }

        return new StashApplyRequested(tabId, selection.CursorStashCommit(), action == ActionIds.StashPop);
    }
}
