using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;

namespace Gitunia.Tui.App.Worktrees;

/// <summary>
/// The terminal side of the worktree actions (ux-workflows.md, workflow 6): it turns the worktree keys into
/// <see cref="WorktreeCommand"/>s for the active tab. It decides nothing; the reducer does, and the runner performs the
/// effects. The prompts and dialogs it leads to are the shared prompt and confirm model.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class WorktreeActions(Dispatcher host, IWorktreeSelectionSource selection) : IActionHandler
{
    /// <summary>Runs the action when it is a worktree action; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action)
    {
        if (host.Workspace.ActiveTabId is not { } tabId)
        {
            return false;
        }

        var command = Request(tabId, action);
        if (command is null)
        {
            return false;
        }

        host.Dispatch(command);

        return true;
    }

    bool IActionHandler.TryExecute(ActionId action, int? count) => TryExecute(action);

    private WorktreeCommand? Request(TabId tabId, ActionId action)
    {
        if (action == ActionIds.WorktreePrune)
        {
            return new WorktreePruneRequested(tabId);
        }

        if (action != ActionIds.WorktreeAdd && action != ActionIds.WorktreeOpen
            && action != ActionIds.WorktreeSwitch && action != ActionIds.WorktreeRemove
            && action != ActionIds.WorktreeLock && action != ActionIds.WorktreeUnlock)
        {
            return null;
        }

        var target = selection.Current();
        if (action == ActionIds.WorktreeAdd)
        {
            return new WorktreeAddRequested(tabId, target);
        }

        if (action == ActionIds.WorktreeOpen)
        {
            return new WorktreeOpenRequested(tabId, target);
        }

        if (action == ActionIds.WorktreeSwitch)
        {
            return new WorktreeSwitchRequested(tabId, target);
        }

        if (action == ActionIds.WorktreeRemove)
        {
            return new WorktreeRemoveRequested(tabId, target);
        }

        if (action == ActionIds.WorktreeLock)
        {
            return new WorktreeLockRequested(tabId, target);
        }

        return new WorktreeUnlockRequested(tabId, target);
    }
}
