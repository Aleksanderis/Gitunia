using Gitunia.App.Status;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App;

/// <summary>
/// The actions that deal with the status message, the failed detail load and the merge parent switch: dismiss, Esc
/// dismiss, retry and the parent of a merge commit. Kept apart from the panes so they run without a
/// terminal.
/// </summary>
/// <param name="statuses">The channel whose latest message is dismissed.</param>
/// <param name="sessions">The active tab's session, null when there is none.</param>
public sealed class StatusActionHandler(StatusReporter statuses, IActiveSessions sessions)
    : IActionHandler, IActionNotice
{
    /// <summary>A short notice for the status bar from the last handled action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    string IActionNotice.Notice => Message;

    bool IActionHandler.TryExecute(ActionId action, int? count) => TryHandle(action);

    /// <summary>Handles the actions that apply before any pane sees them (dismiss, retry); false for every other action.</summary>
    public bool TryHandle(ActionId action)
    {
        Message = string.Empty;
        if (action == ActionIds.StatusDismiss)
        {
            statuses.Dismiss();
            return true;
        }

        if (action == ActionIds.DetailRetry)
        {
            var session = sessions.ActiveSession();
            var retried = session?.Details.Retry() == true;
            retried |= session?.Tree?.Store.Retry() == true;
            Message = retried ? "retrying..." : "nothing to retry";
            return true;
        }

        if (action == ActionIds.DiffMergeParent)
        {
            var details = sessions.ActiveSession()?.Details;
            var parents = details?.Current.Details?.Parents.Count ?? 0;
            Message = "Not a merge commit (or still loading)";
            if (details?.CycleParent() is { } index)
            {
                Message = $"Switching to parent {index + 1} of {parents}...";
            }

            return true;
        }

        return false;
    }

    /// <summary>Handles Esc when nothing else claimed it: it clears the status message. False for every other action.</summary>
    public bool TryHandleFallback(ActionId action)
    {
        Message = string.Empty;
        if (action != ActionIds.Cancel)
        {
            return false;
        }

        statuses.Dismiss();
        return true;
    }
}
