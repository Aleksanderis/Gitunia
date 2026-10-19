using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
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
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs fetch, pull and push.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="refresh">Asks a tab to read everything again; false when it cannot.</param>
/// <param name="redraw">Redraws the chrome (progress line, chips).</param>
/// <param name="gate">The one-operation-per-tab gate, shared with the staging actions; a pull holds it.</param>
public sealed class RemoteActions(
    WorkspaceHost host,
    IRemoteWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<TabId, bool> refresh,
    Action redraw,
    TabOperationGate gate)
{
    private const string BusyNotice = "Busy: a fetch, pull or push of this tab is still running";
    private const string GateBusyNotice = "Busy: another operation of this tab is still running";

    private readonly Dictionary<TabId, Running> _running = [];

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
        var running = new Running(WordsOf(action), linked, plan);
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
        }

        postToUi(() => Finish(tabId, running, action, text, severity));
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

    private void Finish(TabId tabId, Running running, ActionId action, string text, StatusSeverity severity)
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
        }

        redraw();
    }

    private sealed record Wording(string Activity, string Noun);

    private sealed record PushPlan(PushRequest Request, string Done);

    private sealed class Running(Wording words, CancellationTokenSource cancellation, PushPlan? plan)
    {
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
