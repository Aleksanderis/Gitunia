using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>
/// The view side of the workspace-level actions (tabs, pinning, the repository picker): asks the reducer whether the
/// action is its own and dispatches it with the facts only the terminal layer knows (unsent commit messages). Anything
/// else is left to the <see cref="ActionRouter"/>: <see cref="TryExecute"/> returns false for actions it does not own.
/// </summary>
/// <param name="host">Holds the state and takes the commands.</param>
public sealed class WorkspaceActions(Dispatcher host)
{
    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message => host.State.Shell.TabActions.Notice;

    public bool TryExecute(ActionId action, int? count)
    {
        var owns = WorkspaceActionReducer.Owns(action, host.State);
        var session = host.State.Shell.TabActions;
        if (!owns && session.Notice.Length == 0 && session.CloseArmedFor is null)
        {
            return false;
        }

        // The notice and the close warning end with the next action, also one that is not ours.
        var activeHasUnsent = host.Workspace.ActiveTabId is { } active && CommitRules.HasUnsentMessage(host.Workspace, active);
        host.Dispatch(
            new WorkspaceActionPressed(
                action,
                count,
                activeHasUnsent,
                CommitRules.HasAnyUnsentMessage(host.Workspace),
                host.Clock.GetUtcNow()));

        return owns;
    }
}
