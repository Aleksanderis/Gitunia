using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Remote;

/// <summary>
/// Fetch, fetch of all remotes, pull and push of the active tab's current branch, and the cancel of the one that runs
/// (ux.md, workflow 5). Each runs off the UI thread on the tab's own repository and reports git's progress as one
/// status bar line (<see cref="ProgressLine"/>), not as status messages, so the message history stays clean. One remote
/// operation per tab; other tabs stay usable. <c>Esc</c> (the cancel action) or the cancel chip stops the running one:
/// the writer kills git, which leaves the repository consistent. A pull also takes the tab's operation gate because
/// it moves the branch and the working tree. The tab is read again afterwards in every case, as refs may have moved.
/// The background run cannot prompt (no terminal prompts, ssh in batch mode): when it fails for lack of credentials, a
/// passphrase or a host key confirmation, a Confirm offers the same command again with the real terminal handed to git
/// (the TUI suspends like for <c>$EDITOR</c>, #45).
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs fetch, pull and push.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="refresh">Asks a tab to read everything again; false when it cannot.</param>
/// <param name="redraw">Redraws the chrome (progress line, chips).</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the staging actions; a pull holds it.</param>
/// <param name="confirm">Shows the retry Confirm and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="isOverlayOpen">
/// Whether a prompt, picker or other overlay is open; the retry Confirm never replaces one (a stray <c>y</c>).
/// </param>
/// <param name="runInTerminal">
/// Runs git with these arguments on the real terminal (name for the status bar, arguments, repository); returns a
/// notice when it cannot start, empty otherwise.
/// </param>
public sealed class RemoteActions(
    WorkspaceHost host,
    IRemoteWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, bool> refresh,
    Action redraw,
    TabOperationGate gate,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<bool> isOverlayOpen,
    Func<string, IReadOnlyList<string>, RepositoryLocation, string> runInTerminal)
{
    private const string BusyNotice = "Busy: a fetch, pull or push of this tab is still running";
    private const string GateBusyNotice = "Busy: another operation of this tab is still running";

    private readonly Dictionary<TabId, Running> _running = [];

    /// <summary>
    /// Called (on the UI thread, tab active, no overlay open) after a plain push of the tab was rejected as a
    /// non-fast-forward; the owner of the force push answers with its Confirm (#746).
    /// </summary>
    public Action<TabId, string>? PushRejected { get; set; }

    /// <summary>The same for the first push of a branch (set upstream); the last argument is the remote pushed to.</summary>
    public Action<TabId, string, string>? FirstPushRejected { get; set; }

    /// <summary>The notice for the status bar after the last <see cref="TryExecute"/>, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>Whether a fetch, pull or push of the tab is running.</summary>
    public bool IsRunning(TabId tabId) => _running.ContainsKey(tabId);

    /// <summary>
    /// The status bar line of the tab's running operation (<c>Fetching: Receiving objects 45%</c>), empty when none.
    /// </summary>
    public string ProgressLine(TabId tabId)
    {
        if (!_running.TryGetValue(tabId, out var running))
        {
            return string.Empty;
        }

        var line = RemoteProgressText.Format(running.Words.Activity, running.Latest);
        return running.IsCancelling ? $"{line} (cancelling)" : line;
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.Cancel)
        {
            // Esc is only ours while something runs; otherwise it keeps its other meanings.
            return host.Workspace.ActiveTabId is { } active && _running.ContainsKey(active) && Cancel(active);
        }

        if (action == ActionIds.RemoteCancel)
        {
            var cancelled = host.Workspace.ActiveTabId is { } tabId && _running.ContainsKey(tabId) && Cancel(tabId);
            Notice = cancelled ? string.Empty : "No fetch, pull or push is running";
            return true;
        }

        if (action != ActionIds.RemoteFetch && action != ActionIds.RemoteFetchAll
            && action != ActionIds.RemotePull && action != ActionIds.RemotePush)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is { } tab)
        {
            Notice = Start(tab, action, null);
        }

        return true;
    }

    /// <summary>
    /// Starts a push with an explicit request (set the upstream, force with a lease) as the tab's one remote
    /// operation; returns the notice when it cannot start, empty otherwise. <paramref name="done"/> is the status text
    /// of a success.
    /// </summary>
    public string StartPush(RepositoryState tab, PushRequest request, string done) =>
        Start(tab, ActionIds.RemotePush, new PushPlan(request, done));

    private static Wording WordsOf(ActionId action)
    {
        if (action == ActionIds.RemotePull)
        {
            return new Wording("Pulling", "Pull");
        }

        return action == ActionIds.RemotePush ? new Wording("Pushing", "Push") : new Wording("Fetching", "Fetch");
    }

    private static IReadOnlyList<string> ArgumentsForTerminal(ActionId action, PushPlan? plan)
    {
        if (action == ActionIds.RemotePull)
        {
            return RemoteCommands.BuildPull();
        }

        if (action == ActionIds.RemotePush)
        {
            return RemoteCommands.BuildPush(plan?.Request ?? new PushRequest(null), false);
        }

        return RemoteCommands.BuildFetch(
            new FetchRequest(null, action == ActionIds.RemoteFetchAll, true),
            false);
    }

    private bool Cancel(TabId tabId)
    {
        var running = _running[tabId];
        running.IsCancelling = true;
        running.Cancellation.Cancel();
        redraw();
        return true;
    }

    private string Start(RepositoryState tab, ActionId action, PushPlan? plan)
    {
        if (_running.ContainsKey(tab.Id))
        {
            return BusyNotice;
        }

        var isPull = action == ActionIds.RemotePull;
        if (isPull && gate.IsBusy(tab.Id))
        {
            return GateBusyNotice;
        }

        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationOf(tab.Id));
        var current = tab.Refs.FirstOrDefault(item => item.Kind == RefKind.LocalBranch && item.IsCurrent);
        var running = new Running(WordsOf(action), linked, plan, ArgumentsForTerminal(action, plan))
        {
            Branch = current?.Name ?? string.Empty,
        };
        _running[tab.Id] = running;
        if (isPull)
        {
            gate.Begin(tab.Id);
        }

        var progress = new UiProgress(report => postToUi(() => OnProgress(tab.Id, running, report)));
        var location = tab.Location;
        var token = running.Cancellation.Token;
        _ = Task.Run(() => RunAsync(tab.Id, running, action, location, progress, token), CancellationToken.None);
        redraw();
        return string.Empty;
    }

    private async Task RunAsync(
        TabId tabId,
        Running running,
        ActionId action,
        RepositoryLocation location,
        IProgress<RemoteProgress> progress,
        CancellationToken token)
    {
        string text;
        var severity = StatusSeverity.Info;
        var needsTerminal = false;
        var rejected = false;
        try
        {
            text = await RunGitAsync(action, running.Plan, location, progress, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A closed tab has nobody to tell; a cancel by the user is reported.
            text = running.IsCancelling ? $"{running.Words.Noun} cancelled" : string.Empty;
            severity = StatusSeverity.Warning;
        }
        catch (Exception exception)
        {
            text = $"{running.Words.Noun} failed: {RemoteFailureText.Reason(exception)}";
            severity = StatusSeverity.Error;
            needsTerminal = exception is GitCommandFailedException failed
                && RemoteAuthFailure.Matches(failed.StandardError);
            rejected = action == ActionIds.RemotePush && RemoteFailureText.IsNonFastForward(exception)
                && (running.Plan is null || running.Plan.Request is { SetUpstream: true, Lease: null });
        }

        postToUi(() => Finish(tabId, running, action, text, severity, needsTerminal, rejected));
    }

    private async Task<string> RunGitAsync(
        ActionId action,
        PushPlan? plan,
        RepositoryLocation location,
        IProgress<RemoteProgress> progress,
        CancellationToken token)
    {
        if (action == ActionIds.RemotePull)
        {
            var outcome = await writer.PullAsync(location, progress, token).ConfigureAwait(false);
            return outcome == PullOutcome.UpToDate ? "Already up to date" : "Pulled (fast-forward)";
        }

        if (action == ActionIds.RemotePush)
        {
            var request = plan?.Request ?? new PushRequest(null);
            await writer.PushAsync(location, request, progress, token).ConfigureAwait(false);
            return plan?.Done ?? "Pushed the current branch";
        }

        var all = action == ActionIds.RemoteFetchAll;
        await writer.FetchAsync(location, new FetchRequest(null, all, true), progress, token).ConfigureAwait(false);
        return all ? "Fetched all remotes" : "Fetched the remote of the current branch";
    }

    private void OnProgress(TabId tabId, Running running, RemoteProgress report)
    {
        if (_running.TryGetValue(tabId, out var current) && current == running)
        {
            running.Latest = report;
            redraw();
        }
    }

    private void Finish(
        TabId tabId,
        Running running,
        ActionId action,
        string text,
        StatusSeverity severity,
        bool needsTerminal,
        bool rejected)
    {
        running.Cancellation.Dispose();
        _running.Remove(tabId);
        if (action == ActionIds.RemotePull)
        {
            gate.End(tabId);
        }

        if (text.Length > 0)
        {
            statuses.Post(text, severity);
        }

        if (host.Workspace.FindTab(tabId) is not null)
        {
            refresh(tabId);
            if (needsTerminal)
            {
                OfferTerminal(tabId, running);
            }
            else if (rejected && host.Workspace.ActiveTabId == tabId && !isOverlayOpen())
            {
                if (running.Plan?.Request is { Remote: { } remoteName })
                {
                    FirstPushRejected?.Invoke(tabId, running.Branch, remoteName);
                }
                else
                {
                    PushRejected?.Invoke(tabId, running.Branch);
                }
            }
        }

        redraw();
    }

    /// <summary>
    /// Offers the terminal retry for a git command that failed for lack of credentials (also used by the remote branch
    /// delete and the tag pushes, which run outside this class). <paramref name="noun"/> names the operation
    /// ("Tag push"); <paramref name="arguments"/> are exactly what failed (built by the same
    /// <see cref="RemoteCommands"/> method as the writer, minus background-only options);
    /// <paramref name="isCallerBusy"/> is re-checked on <c>y</c> so the terminal never races a newer operation of the
    /// caller.
    /// </summary>
    public void OfferTerminalRetry(
        TabId tabId,
        string noun,
        IReadOnlyList<string> arguments,
        Func<bool> isCallerBusy)
    {
        if (host.Workspace.FindTab(tabId) is not null)
        {
            Offer(tabId, noun, arguments, isCallerBusy);
        }
    }

    private void OfferTerminal(TabId tabId, Running running) =>
        Offer(tabId, running.Words.Noun, running.TerminalArguments, () => false);

    private void Offer(TabId tabId, string noun, IReadOnlyList<string> arguments, Func<bool> isCallerBusy)
    {
        var command = $"git {string.Join(' ', arguments)}";
        var repository = Path.GetFileName(host.Workspace.FindTab(tabId)!.Location.RootPath);

        // An unsolicited Confirm must not replace something the user is typing in or belong to a tab they left.
        var canAsk = host.Workspace.ActiveTabId == tabId && !isOverlayOpen();
        var prompt = new ConfirmPrompt(
            "Credentials needed",
            [
                $"{noun} in {repository} needs a passphrase, credentials or a host key answer, "
                + "which the background run cannot give.",
                "Run it again in the terminal? Gitunia hands the terminal to git and takes it back when git ends.",
            ],
            $"Runs: {command} (the same command as the one that failed).",
            "Retry in terminal");
        if (!canAsk || !confirm(prompt, () => RetryInTerminal(tabId, noun, arguments, isCallerBusy)))
        {
            statuses.Warning(
                $"{noun} in {repository} needs credentials: run `{command}` in a terminal");
        }
    }

    private void RetryInTerminal(
        TabId tabId,
        string noun,
        IReadOnlyList<string> arguments,
        Func<bool> isCallerBusy)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab)
        {
            return;
        }

        // Another operation may have started while the Confirm was open; the terminal must not race it.
        if (_running.ContainsKey(tabId) || gate.IsBusy(tabId) || isCallerBusy())
        {
            statuses.Warning(BusyNotice);
            return;
        }

        var notice = runInTerminal(noun.ToLowerInvariant(), arguments, tab.Location);
        if (notice.Length > 0)
        {
            statuses.Warning(notice);
        }
    }

    private sealed record Wording(string Activity, string Noun);

    private sealed record PushPlan(PushRequest Request, string Done);

    private sealed class Running(
        Wording words,
        CancellationTokenSource cancellation,
        PushPlan? plan,
        IReadOnlyList<string> terminalArguments)
    {
        public IReadOnlyList<string> TerminalArguments { get; } = terminalArguments;

        /// <summary>The branch that was checked out when the operation started.</summary>
        public string Branch { get; init; } = string.Empty;

        public PushPlan? Plan { get; } = plan;

        public Wording Words { get; } = words;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public RemoteProgress? Latest { get; set; }

        public bool IsCancelling { get; set; }
    }

    private sealed class UiProgress(Action<RemoteProgress> report) : IProgress<RemoteProgress>
    {
        public void Report(RemoteProgress value) => report(value);
    }
}
