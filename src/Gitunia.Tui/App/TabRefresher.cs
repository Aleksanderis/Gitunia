using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Loading;

namespace Gitunia.Tui.App;

/// <summary>
/// Keeps one tab current after its initial load: owns the tab's <see cref="RepositoryWatcher"/>, re-reads only what a change can
/// affect, and applies the sections that differ as commands. The graph is replaced only when HEAD or refs changed (or on a manual
/// refresh); an automatic reload reads the log only down to the first row it shares with the old graph (<see cref="IncrementalGraphLoader"/>), and the view puts selection and scroll back. Runs one refresh at a time; changes arriving meanwhile are merged into the next.
/// Touched from the UI thread only; the watcher callback posts to it. The optional <c>graphReloaded</c> callback runs on the UI thread after each graph replacement, so tests can tell an incremental reload (<see cref="GraphReload.ReusedCount"/> above 0) from a full read.
/// </summary>
public sealed class TabRefresher : IDisposable
{
    private readonly GitRunner runner;
    private readonly WorkspaceHost host;
    private readonly TabSession session;
    private readonly RepositoryLocation location;
    private readonly StatusChannel statuses;
    private readonly Action<Action> postToUi;
    private readonly RepositoryWatcher watcher;
    private readonly Action<GraphReload>? graphReloaded;
    private RepositorySnapshot? snapshot;
    private RepositoryChangeKinds pending;
    private bool forceGraph;
    private bool announce;
    private bool isReady;
    private bool sawChange;
    private bool isRunning;
    private bool isDisposed;
    private string lastError = string.Empty;

    public TabRefresher(GitRunner runner, WorkspaceHost host, TabSession session, RepositoryLocation location, StatusChannel statuses, Action<Action> postToUi, WatchBudget budget, RepositoryWatcherOptions options, Action<GraphReload>? graphReloaded = null)
    {
        this.graphReloaded = graphReloaded;
        this.runner = runner;
        this.host = host;
        this.session = session;
        this.location = location;
        this.statuses = statuses;
        this.postToUi = postToUi;
        watcher = new RepositoryWatcher(location, budget, options, kinds => postToUi(() => Request(kinds, false)));
    }

    /// <summary>A refresh is being read or applied right now.</summary>
    public bool IsRefreshing => isRunning;

    /// <summary>Starts watching; changes are collected now and applied once the initial load has finished.</summary>
    public void Start()
    {
        watcher.Start();
        if (session.IsPending || session.IsLoading)
        {
            session.LoadFinished += OnLoadFinished;
        }
        else
        {
            OnLoadFinished();
        }
    }

    /// <summary>Marks the tab visible or hidden; a hidden tab reports its pending changes when shown again.</summary>
    public void SetActive(bool isActive) => watcher.SetActive(isActive);

    /// <summary>Manual refresh: reads everything and reloads the graph.</summary>
    public void RefreshNow()
    {
        announce = true;
        Request(RepositoryChangeKinds.All, true);
    }

    public void Dispose()
    {
        isDisposed = true;
        session.LoadFinished -= OnLoadFinished;
        watcher.Dispose();
    }

    private void OnLoadFinished()
    {
        session.LoadFinished -= OnLoadFinished;
        isReady = true;
        RunNext();
    }

    private void Request(RepositoryChangeKinds kinds, bool reloadGraph)
    {
        if (isDisposed)
        {
            return;
        }

        pending |= kinds;
        forceGraph |= reloadGraph;
        sawChange = true;
        RunNext();
    }

    private void RunNext()
    {
        if (!isReady || isRunning || isDisposed || (snapshot is not null && pending == RepositoryChangeKinds.None))
        {
            return;
        }

        var kinds = pending;
        var isManual = announce;
        announce = false;
        var reloadGraph = forceGraph || (snapshot is null && (sawChange || watcher.IsStale));
        pending = RepositoryChangeKinds.None;
        forceGraph = false;
        isRunning = true;
        var previous = snapshot;
        var token = session.Cancellation.Token;

        // A copy, because the background read must not see the graph change; only a refresh that is not forced may reuse the old rows
        // (the baseline needs them too, to compare the graph with its snapshot).
        IReadOnlyList<GraphEntry> previousRows = reloadGraph ? [] : session.Graph.ToArray();
        _ = Task.Run(() => RefreshAsync(previous, previousRows, kinds, reloadGraph, isManual, token), CancellationToken.None);
    }

    private async Task RefreshAsync(RepositorySnapshot? previous, IReadOnlyList<GraphEntry> previousRows, RepositoryChangeKinds kinds, bool reloadGraph, bool isManual, CancellationToken token)
    {
        try
        {
            var repo = new Repo(runner, location);
            var result = await RepositoryRefresher.RefreshAsync(repo, previous, kinds, token).ConfigureAwait(false);
            var refsMoved = kinds.HasFlag(RepositoryChangeKinds.Head) || kinds.HasFlag(RepositoryChangeKinds.Refs) || previous is null;
            var stashes = refsMoved ? (await StashReader.ReadStashesAsync(repo, token).ConfigureAwait(false)).Items : null;
            GraphReload? reload = null;

            // The first pass is the baseline: it re-applies every section (the tab may have changed since the initial load read it) and reloads the graph
            // when a change was seen meanwhile or when the loaded graph no longer matches the baseline refs (a change between the load and this read leaves no event to diff).
            var graphChanged = previous is null
                ? reloadGraph || GraphRefCheck.Differs(previousRows, result.Snapshot)
                : result.Changed.HasFlag(RepositorySections.Head) || result.Changed.HasFlag(RepositorySections.Refs) || reloadGraph;
            if (graphChanged)
            {
                reload = await IncrementalGraphLoader.ReloadAsync(repo, previousRows, token).ConfigureAwait(false);
            }

            postToUi(() => Apply(previous is null, isManual, result, stashes, reload));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            postToUi(() => Fail(exception, kinds, reloadGraph, isManual));
        }
    }

    private void Apply(bool isBaseline, bool isManual, RepositoryRefreshResult result, IReadOnlyList<StashEntry>? stashes, GraphReload? reload)
    {
        isRunning = false;
        if (isDisposed)
        {
            return;
        }

        lastError = string.Empty;
        snapshot = result.Snapshot;
        var tabId = session.TabId;
        {
            var changed = isBaseline ? RepositorySections.All : result.Changed;
            if (changed.HasFlag(RepositorySections.Head))
            {
                host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(result.Snapshot.Head)));
            }

            if (changed.HasFlag(RepositorySections.Refs))
            {
                host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. result.Snapshot.Refs])));
            }

            if (changed.HasFlag(RepositorySections.Worktrees))
            {
                host.Dispatch(new TabCommand(tabId, new WorktreesLoadedCommand([.. result.Snapshot.Worktrees])));
            }

            if (changed.HasFlag(RepositorySections.Status))
            {
                var status = result.Snapshot.Status;
                var summary = RepoStatusSummary.From(status);
                host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary)));
            }

            if (stashes is not null && host.Workspace.FindTab(tabId) is { } tab && !tab.Stashes.SequenceEqual(stashes))
            {
                host.Dispatch(new TabCommand(tabId, new StashesLoadedCommand([.. stashes])));
            }
        }

        if (reload is not null)
        {
            session.ReplaceGraph(reload.Entries);
            graphReloaded?.Invoke(reload);
        }

        if (isManual)
        {
            statuses.Post(RepositoryName() + ": refreshed");
        }

        RunNext();
    }

    private void Fail(Exception exception, RepositoryChangeKinds kinds, bool reloadGraph, bool isManual)
    {
        isRunning = false;
        pending |= kinds;
        forceGraph |= reloadGraph;
        announce |= isManual;
        if (isDisposed)
        {
            return;
        }

        // A repository that keeps failing (polling every few seconds) is reported once, not on every tick.
        var message = RepositoryName() + ": could not refresh: " + exception.Message;
        if (isManual || message != lastError)
        {
            lastError = message;
            statuses.Error(message);
        }
    }

    private string RepositoryName() => Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath));
}
