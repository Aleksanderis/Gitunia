using Gitunia.Core.Keymap;
using Gitunia.Core.Status;

namespace Gitunia.Tui.App;

/// <summary>
/// The actions that deal with the status message and the failed detail load: dismiss, Esc dismiss and retry. Kept out
/// of the <see cref="ActionRouter"/> so they run without a terminal.
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
            Message = activeSession()?.Details.Retry() == true ? "retrying..." : "nothing to retry";
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
