using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.Loading.Graph;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// Keeps one tab current after its initial load: owns the tab's <see cref="RepositoryWatcher"/>, re-reads only what a change can
/// affect, and applies the sections that differ as commands. The graph is replaced only when HEAD or refs changed (or on a manual
/// refresh); an automatic reload reads the log only down to the first row it shares with the old graph (<see cref="IncrementalGraphLoader"/>), and the view puts selection and scroll back. Runs one refresh at a time; changes arriving meanwhile are merged into the next.
/// Touched from the UI thread only; the watcher callback posts to it. The optional <c>graphReloaded</c> callback runs on the UI thread after each graph replacement, so tests can tell an incremental reload (<see cref="GraphReload.ReusedCount"/> above 0) from a full read.
/// </summary>
public sealed class TabRefresher : IDisposable
{
    private readonly IRepositoryRefresher _refresher;
    private readonly IStashReader _stashes;
    private readonly IncrementalGraphLoader _graphLoader;
    private readonly WorkspaceHost _host;
    private readonly TabSession _session;
    private readonly RepositoryLocation _location;
    private readonly StatusChannel _statuses;
    private readonly Action<Action> _postToUi;
    private readonly RepositoryWatcher _watcher;
    private readonly Action<GraphReload>? _graphReloaded;
    private RepositorySnapshot? _snapshot;
    private RepositoryChangeKinds _pending;
    private bool _forceGraph;
    private bool _announce;
    private bool _isReady;
    private bool _sawChange;
    private bool _isRunning;
    private bool _isDisposed;
    private string _lastError = string.Empty;

    public TabRefresher(IRepositoryRefresher refresher, IStashReader stashes, IncrementalGraphLoader graphLoader, WorkspaceHost host, TabSession session, RepositoryLocation location, StatusChannel statuses, Action<Action> postToUi, WatchBudget budget, RepositoryWatcherOptions options, Action<GraphReload>? graphReloaded = null)
    {
        _graphReloaded = graphReloaded;
        _refresher = refresher;
        _stashes = stashes;
        _graphLoader = graphLoader;
        _host = host;
        _session = session;
        _location = location;
        _statuses = statuses;
        _postToUi = postToUi;
        _watcher = new RepositoryWatcher(location, budget, options, kinds => postToUi(() => Request(kinds, false)));
    }

    /// <summary>A refresh is being read or applied right now.</summary>
    public bool IsRefreshing => _isRunning;

    /// <summary>Starts watching; changes are collected now and applied once the initial load has finished.</summary>
    public void Start()
    {
        _watcher.Start();
        if (_session.IsPending || _session.IsLoading)
        {
            _session.LoadFinished += OnLoadFinished;
        }
        else
        {
            OnLoadFinished();
        }
    }

    /// <summary>Marks the tab visible or hidden; a hidden tab reports its pending changes when shown again.</summary>
    public void SetActive(bool isActive) => _watcher.SetActive(isActive);

    /// <summary>Manual refresh: reads everything and reloads the graph.</summary>
    public void RefreshNow()
    {
        _announce = true;
        Request(RepositoryChangeKinds.All, true);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _session.LoadFinished -= OnLoadFinished;
        _watcher.Dispose();
    }

    private void OnLoadFinished()
    {
        _session.LoadFinished -= OnLoadFinished;
        _isReady = true;
        RunNext();
    }

    private void Request(RepositoryChangeKinds kinds, bool reloadGraph)
    {
        if (_isDisposed)
        {
            return;
        }

        _pending |= kinds;
        _forceGraph |= reloadGraph;
        _sawChange = true;
        RunNext();
    }

    private void RunNext()
    {
        if (!_isReady || _isRunning || _isDisposed || (_snapshot is not null && _pending == RepositoryChangeKinds.None))
        {
            return;
        }

        var kinds = _pending;
        var isManual = _announce;
        _announce = false;
        var reloadGraph = _forceGraph || (_snapshot is null && (_sawChange || _watcher.IsStale));
        _pending = RepositoryChangeKinds.None;
        _forceGraph = false;
        _isRunning = true;
        var previous = _snapshot;
        var token = _session.Cancellation.Token;

        // A copy, because the background read must not see the graph change; only a refresh that is not forced may reuse the old rows
        // (the baseline needs them too, to compare the graph with its snapshot).
        IReadOnlyList<GraphEntry> previousRows = reloadGraph ? [] : _session.Graph.ToArray();
        _ = Task.Run(() => RefreshAsync(previous, previousRows, kinds, reloadGraph, isManual, token), CancellationToken.None);
    }

    private async Task RefreshAsync(RepositorySnapshot? previous, IReadOnlyList<GraphEntry> previousRows, RepositoryChangeKinds kinds, bool reloadGraph, bool isManual, CancellationToken token)
    {
        try
        {
            var result = await _refresher.RefreshAsync(_location, previous, kinds, token).ConfigureAwait(false);
            var refsMoved = kinds.HasFlag(RepositoryChangeKinds.Head) || kinds.HasFlag(RepositoryChangeKinds.Refs) || previous is null;
            var stashes = refsMoved ? (await _stashes.ReadStashesAsync(_location, token).ConfigureAwait(false)).Items : null;
            GraphReload? reload = null;

            // The first pass is the baseline: it re-applies every section (the tab may have changed since the initial load read it) and reloads the graph
            // when a change was seen meanwhile or when the loaded graph no longer matches the baseline refs (a change between the load and this read leaves no event to diff).
            var graphChanged = previous is null
                ? reloadGraph || GraphRefCheck.Differs(previousRows, result.Snapshot)
                : result.Changed.HasFlag(RepositorySections.Head) || result.Changed.HasFlag(RepositorySections.Refs) || reloadGraph;
            if (graphChanged)
            {
                reload = await _graphLoader.ReloadAsync(_location, previousRows, token).ConfigureAwait(false);
            }

            _postToUi(() => Apply(previous is null, isManual, result, stashes, reload));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            _postToUi(() => Fail(exception, kinds, reloadGraph, isManual));
        }
    }

    private void Apply(bool isBaseline, bool isManual, RepositoryRefreshResult result, IReadOnlyList<StashEntry>? stashes, GraphReload? reload)
    {
        _isRunning = false;
        if (_isDisposed)
        {
            return;
        }

        _lastError = string.Empty;
        _snapshot = result.Snapshot;
        var tabId = _session.TabId;
        {
            var changed = isBaseline ? RepositorySections.All : result.Changed;
            if (changed.HasFlag(RepositorySections.Head))
            {
                _host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(result.Snapshot.Head)));
            }

            if (changed.HasFlag(RepositorySections.Refs))
            {
                _host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. result.Snapshot.Refs])));
            }

            if (changed.HasFlag(RepositorySections.Worktrees))
            {
                _host.Dispatch(new TabCommand(tabId, new WorktreesLoadedCommand([.. result.Snapshot.Worktrees])));
            }

            if (changed.HasFlag(RepositorySections.Status))
            {
                var status = result.Snapshot.Status;
                var summary = RepositoryStatusSummary.From(status);
                _host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary)));
            }

            if (stashes is not null && _host.Workspace.FindTab(tabId) is { } tab && !tab.Stashes.SequenceEqual(stashes))
            {
                _host.Dispatch(new TabCommand(tabId, new StashesLoadedCommand([.. stashes])));
            }
        }

        if (reload is not null)
        {
            _session.ReplaceGraph(reload.Entries);
            _graphReloaded?.Invoke(reload);
        }

        if (isManual)
        {
            _statuses.Post($"{RepositoryName()}: refreshed");
        }

        RunNext();
    }

    private void Fail(Exception exception, RepositoryChangeKinds kinds, bool reloadGraph, bool isManual)
    {
        _isRunning = false;
        _pending |= kinds;
        _forceGraph |= reloadGraph;
        _announce |= isManual;
        if (_isDisposed)
        {
            return;
        }

        // A repository that keeps failing (polling every few seconds) is reported once, not on every tick.
        var message = $"{RepositoryName()}: could not refresh: {exception.Message}";
        if (isManual || message != _lastError)
        {
            _lastError = message;
            _statuses.Error(message);
        }
    }

    private string RepositoryName() => Path.GetFileName(Path.TrimEndingDirectorySeparator(_location.RootPath));
}
