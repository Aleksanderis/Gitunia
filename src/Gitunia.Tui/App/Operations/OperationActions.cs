using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Integration;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App.Operations;

/// <summary>
/// The terminal side of continue, abort and skip for the operation in progress (ux-workflows.md, workflow 4): it turns
/// the keys into an <see cref="OperationRequested"/> for the active tab. It decides nothing; the reducer refuses, asks
/// or starts the call and the runner performs it.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
public sealed class OperationActions(Dispatcher host) : IActionHandler
{
    /// <summary>Runs the action when it is continue, abort or skip; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        if (ActionOf(action) is not { } requested)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is { } tabId)
        {
            host.Dispatch(new OperationRequested(tabId, requested));
        }

        return true;
    }

    private static OperationAction? ActionOf(ActionId action)
    {
        if (action == ActionIds.OperationContinue)
        {
            return OperationAction.Continue;
        }

        if (action == ActionIds.OperationAbort)
        {
            return OperationAction.Abort;
        }

        return action == ActionIds.OperationSkip ? OperationAction.Skip : null;
    }
}
