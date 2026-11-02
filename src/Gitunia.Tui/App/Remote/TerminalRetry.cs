using Gitunia.App;
using Gitunia.App.Status;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// The credential terminal retry (#45): the background run of a git call cannot prompt (no terminal prompts, ssh in batch
/// mode), so when it failed for lack of credentials, a passphrase or a host key confirmation, a Confirm offers the same
/// command again with the real terminal handed to git (the TUI suspends like for <c>$EDITOR</c>). The reducer decides to
/// offer it (<c>OfferTerminalRetryEffect</c>); this is the dialog and the hand-off.
/// </summary>
/// <param name="host">The workspace and the confirm dialog.</param>
/// <param name="statuses">Where the notices go.</param>
/// <param name="shell">The overlays, the tab activity and the terminal.</param>
public sealed class TerminalRetry(Dispatcher host, StatusReporter statuses, ITerminalRetryHost shell)
{
    private const string BusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>
    /// Offers to run <paramref name="arguments"/> (exactly what failed, minus background-only options) again in the
    /// terminal. <paramref name="noun"/> names the operation (<c>Tag push</c>). Nothing happens for a closed tab.
    /// </summary>
    public void Offer(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        var command = $"git {string.Join(' ', arguments)}";
        var repository = Path.GetFileName(tab.Location.RootPath);

        // An unsolicited Confirm must not replace something the user is typing in or belong to a tab they left.
        var canAsk = host.Workspace.ActiveTabId == tabId && !shell.IsOverlayOpen();
        var prompt = new ConfirmPrompt(
            "Credentials needed",
            [
                $"{noun} in {repository} needs a passphrase, credentials or a host key answer, "
                + "which the background run cannot give.",
                "Run it again in the terminal? Gitunia hands the terminal to git and takes it back when git ends.",
            ],
            $"Runs: {command} (the same command as the one that failed).",
            "Retry in terminal");
        var accepted = new RetryInTerminalConfirmed(tabId, noun, arguments);
        if (!canAsk || !Ask(prompt, accepted))
        {
            statuses.Warning($"{noun} in {repository} needs credentials: run `{command}` in a terminal");
        }
    }

    /// <summary>
    /// Runs <paramref name="arguments"/> in the terminal once the user accepted the dialog (the reducer turns
    /// <c>RetryInTerminalConfirmed</c> into the effect that calls this). Nothing happens for a closed tab or while
    /// another operation of the tab runs.
    /// </summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="noun">What failed.</param>
    /// <param name="arguments">The git arguments.</param>
    public void Run(TabId tabId, string noun, IReadOnlyList<string> arguments)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        // Another operation may have started while the Confirm was open; the terminal must not race it.
        if (tab.Running.IsBusy || shell.IsBusy(tabId))
        {
            statuses.Warning(BusyNotice);
            return;
        }

        var notice = shell.RunGitInTerminal(noun.ToLowerInvariant(), arguments, tab.Location);
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    // False (and nothing shown) when the dialog does not fit the screen.
    private bool Ask(ConfirmPrompt prompt, RetryInTerminalConfirmed accepted)
    {
        host.Dispatch(new ConfirmRequestedCommand(prompt, accepted, null));

        return ReferenceEquals(host.State.Shell.Confirm?.Prompt, prompt);
    }
}
