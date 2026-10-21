using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App;

/// <summary>
/// The actions that deal with the status message, the failed detail load and the merge parent switch: dismiss, Esc
/// dismiss, retry and the parent of a merge commit. Kept out of the <see cref="ActionRouter"/> so they run without a
/// terminal.
/// </summary>
/// <param name="statuses">The channel whose latest message is dismissed.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
public sealed class StatusActionHandler(StatusChannel statuses, Func<TabSession?> activeSession)
{
    /// <summary>A short notice for the status bar from the last handled action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

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
            var session = activeSession();
            var retried = session?.Details.Retry() == true;
            retried |= session?.Tree?.Store.Retry() == true;
            Message = retried ? "retrying..." : "nothing to retry";
            return true;
        }

        if (action == ActionIds.DiffMergeParent)
        {
            var details = activeSession()?.Details;
            var parents = details?.Current.Details?.Parents.Count ?? 0;
            Message = details?.CycleParent() is { } index
                ? $"Changes against parent {index + 1} of {parents}"
                : "Not a merge commit (or still loading)";
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
