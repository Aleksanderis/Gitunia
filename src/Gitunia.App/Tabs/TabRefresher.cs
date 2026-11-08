using System.Diagnostics;
using Gitunia.App.Loading.Graph;
using Gitunia.App.Status;
using Gitunia.App.Tabs.Models;
using Gitunia.Core.Graph;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Config;
using Gitunia.Git.Operations;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App.Tabs;

/// <summary>
/// Keeps one tab current after its initial load: notices changes by polling (<see cref="TabPoller"/>, the default) or,
/// when enabled, with the tab's <see cref="RepositoryWatcher"/>, re-reads only what a change can affect, and applies
/// the sections that differ as commands. The graph is replaced only when HEAD or refs changed (or on a manual
/// refresh); an automatic reload reads the log only down to the first row it shares with the old graph
/// (<see cref="IncrementalGraphLoader"/>), and the view puts selection and scroll back. Runs one refresh at a time;
/// changes arriving meanwhile are merged into the next.
/// Touched from the UI thread only; the watcher and poller callbacks post to it.
/// </summary>
public sealed class TabRefresher : IDisposable, IPollTarget
{
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(5);

    private const RepositoryChangeKinds StatusKinds = RepositoryChangeKinds.Head
        | RepositoryChangeKinds.Refs
        | RepositoryChangeKinds.Index
        | RepositoryChangeKinds.WorkingTree
        | RepositoryChangeKinds.Operation;

    private readonly IRepositoryRefresher _refresher;
    private readonly IStashReader _stashes;
    private readonly IOperationInfoReader _operations;
    private readonly IGitConfigReader? _config;
    private readonly IncrementalGraphLoader _graphLoader;
    private readonly Dispatcher _host;
    private readonly IGraphSession _session;
    private readonly RepositoryLocation _location;
    private readonly StatusReporter _statuses;
    private readonly IUiInvoker _ui;
    private readonly RepositoryWatcher? _watcher;
    private readonly TabPoller? _poller;
    private readonly CancellationTokenSource _stop;
    private readonly ShutdownTasks _tasks = new();
    private readonly ITabBusySource _busy = NeverBusy.Instance;
    private long _refreshStartedAt;
    private RepositorySnapshot? _snapshot;
    private RepositoryChangeKinds _pending;
    private bool _forceGraph;
    private bool _announce;
    private bool _isReady;
    private bool _sawChange;
    private bool _isRunning;
    private bool _isDisposed;
    private string _lastError = string.Empty;
    private List<Action> _waiting = [];
    private List<Action> _inFlight = [];

    public TabRefresher(TabRefreshServices services, TabContext context, TabWatching watching)
    {
        _config = services.Config;
        _operations = services.Operations ?? new OperationInfoReader();
        _refresher = services.Refresher;
        _stashes = services.Stashes;
        _graphLoader = services.GraphLoader;
        _host = context.Host;
        _session = context.Session;
        _location = context.Location;
        _statuses = context.Statuses;
        _ui = context.Ui;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(_session.Cancellation.Token);
        if (watching.Options.UseFileWatchers)
        {
            _watcher = new RepositoryWatcher(
                _location,
                watching.Budget,
                watching.Options,
                directoryLister: services.DirectoryLister);

            // Subscribed before Start, so no change is reported to nobody.
            _watcher.Changed += OnWatcherChanged;
        }
        else
        {
            _busy = watching.Busy ?? NeverBusy.Instance;
            _poller = new TabPoller(_location, watching.Poll ?? PollOptions.Default, _ui, this);
        }
    }

    /// <summary>Runs on the UI thread after each graph replacement, so tests can tell an incremental reload
    /// (<see cref="GraphReload.ReusedCount"/> above 0) from a full read.</summary>
    public event Action<GraphReload>? GraphReloaded;

    bool IPollTarget.CanPoll => _isReady && !_isRunning && _snapshot is not null && !_busy.IsBusy(_session.TabId);

    private void OnWatcherChanged(RepositoryChangeKinds kinds) => _ui.Post(() => Request(kinds, false));

    void IPollTarget.Request(RepositoryChangeKinds kinds) => Request(kinds, false);

    /// <summary>The wait between automatic status refreshes (longer after a slow one); null when watching.</summary>
    public TimeSpan? StatusPollInterval => _poller?.StatusInterval;

    /// <summary>A refresh is being read or applied right now.</summary>
    public bool IsRefreshing => _isRunning;

    /// <summary>Starts watching; changes are collected now and applied once the initial load has finished.</summary>
    public void Start()
    {
        _watcher?.Start();
        _poller?.Start();
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
    public void SetActive(bool isActive)
    {
        _watcher?.SetActive(isActive);
        _poller?.SetActive(isActive);
    }

    /// <summary>The terminal gained focus: polls now, not at the next tick. No effect with file watching.</summary>
    public void CheckNow() => _poller?.CheckNow();

    /// <summary>Manual refresh: reads everything and reloads the graph; <paramref name="announce"/> false keeps the status bar as it is unless it fails.</summary>
    public void RefreshNow(bool announce = true)
    {
        _announce = announce;
        Request(RepositoryChangeKinds.All, true);
    }

    /// <summary>
    /// Re-reads the index-dependent status now (after this app changed the index) and runs <paramref name="applied"/> on the UI thread once a
    /// read that started after this call has been applied or has failed. A read already running does not count: it may predate the change.
    /// </summary>
    public void RefreshStatus(Action applied)
    {
        _waiting.Add(applied);
        Request(RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree, false);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _session.LoadFinished -= OnLoadFinished;
        _watcher?.Dispose();
        _poller?.Dispose();

        // Cancels the running read and waits (bounded) for its git process to end, so the repository directory is free
        // when this returns. Runs at shutdown or tab close.
        _stop.Cancel();
        _tasks.Wait(ShutdownWait);
        _stop.Dispose();
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

        // The rest of a cut log is loading: a reload now would throw it away, so wait for it to end.
        if (_session.IsLoading)
        {
            _isReady = false;
            _session.LoadFinished += OnLoadFinished;

            return;
        }

        var kinds = _pending;
        var isManual = _announce;
        _announce = false;
        var reloadGraph = _forceGraph || (_snapshot is null && (_sawChange || _watcher?.IsStale == true));
        _pending = RepositoryChangeKinds.None;
        _forceGraph = false;
        _isRunning = true;
        _refreshStartedAt = Stopwatch.GetTimestamp();
        _inFlight = _waiting;
        _waiting = [];
        var previous = _snapshot;
        var token = _stop.Token;

        // A copy, because the background read must not see the graph change; only a refresh that is not forced may reuse the old rows
        // (the baseline needs them too, to compare the graph with its snapshot).
        // A cut log reloads its newest rows again instead: the rows below the cut are not loaded, so nothing joins
        // and the count guard cannot hold.
        var isCapped = _session.IsTruncated;
        IReadOnlyList<GraphEntry> previousRows = reloadGraph || isCapped ? [] : _session.Graph.ToArray();
        _tasks.Run(() => RefreshAsync(previous, previousRows, isCapped, kinds, reloadGraph, isManual, token));
    }

    private async Task RefreshAsync(
        RepositorySnapshot? previous,
        IReadOnlyList<GraphEntry> previousRows,
        bool isCapped,
        RepositoryChangeKinds kinds,
        bool reloadGraph,
        bool isManual,
        CancellationToken token)
    {
        try
        {
            var result = await _refresher.RefreshAsync(_location, previous, kinds, token).ConfigureAwait(false);
            var refsMoved = kinds.HasFlag(RepositoryChangeKinds.Head) || kinds.HasFlag(RepositoryChangeKinds.Refs) || previous is null;
            var stashes = refsMoved ? (await _stashes.ReadStashesAsync(_location, token).ConfigureAwait(false)).Items : null;
            // A change of the repository config reads as a refs change; a manual refresh also picks up a global one.
            var autoStash = default(bool?);
            if ((refsMoved || isManual) && _config is not null)
            {
                autoStash = await _config.ReadRebaseAutoStashAsync(_location, token).ConfigureAwait(false);
            }

            var operation = await _operations
                .ReadAsync(_location, result.Snapshot.Operation, token)
                .ConfigureAwait(false);
            GraphReload? reload = null;

            // The first pass is the baseline: it re-applies every section (the tab may have changed since the initial load read it) and reloads the graph
            // when a change was seen meanwhile or when the loaded graph no longer matches the baseline refs (a change between the load and this read leaves no event to diff).
            var graphChanged = result.Changed.HasFlag(RepositorySections.Head)
                || result.Changed.HasFlag(RepositorySections.Refs)
                || reloadGraph;
            if (previous is null)
            {
                graphChanged = reloadGraph || (!isCapped && GraphRefCheck.Differs(previousRows, result.Snapshot));
            }

            if (graphChanged)
            {
                reload = await _graphLoader.ReloadAsync(_location, previousRows, isCapped, token).ConfigureAwait(false);
            }

            _ui.Post(() => Apply(previous is null, isManual, result, stashes, reload, ReadsStatus(kinds), operation, autoStash));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            _ui.Post(() => Fail(exception, kinds, reloadGraph, isManual));
        }
    }

    private void Apply(
        bool isBaseline,
        bool isManual,
        RepositoryRefreshResult result,
        IReadOnlyList<StashEntry>? stashes,
        GraphReload? reload,
        bool readStatus,
        OperationInfo? operation,
        bool? autoStash)
    {
        _isRunning = false;
        var applied = TakeInFlight();
        if (_isDisposed)
        {
            return;
        }

        if (reload is null && !isManual)
        {
            _poller?.RecordRefresh(Stopwatch.GetElapsedTime(_refreshStartedAt));
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
                _host.Dispatch(new TabCommand(tabId, new WorkingTreeLoadedCommand(result.Snapshot.Status)));
            }
            else if (readStatus)
            {
                // The letters did not change, but a file may have (a second edit of a modified file): its diff reads again.
                _session.NotifyStatusRead();
            }

            if (_host.Workspace.FindTab(tabId) is { } operationTab && operationTab.Operation != operation)
            {
                _host.Dispatch(new TabCommand(tabId, new OperationLoadedCommand(operation)));
            }

            if (autoStash is { } isOn && _host.Workspace.FindTab(tabId) is { } autoStashTab
                && autoStashTab.RebaseAutoStash != isOn)
            {
                _host.Dispatch(new TabCommand(tabId, new RebaseAutoStashLoadedCommand(isOn)));
            }

            if (stashes is not null && _host.Workspace.FindTab(tabId) is { } tab && !tab.Stashes.SequenceEqual(stashes))
            {
                _host.Dispatch(new TabCommand(tabId, new StashesLoadedCommand([.. stashes])));
            }
        }

        if (reload is not null)
        {
            _session.ReplaceGraph(reload.Entries, reload.TruncatedTotal);
            GraphReloaded?.Invoke(reload);
        }

        if (isManual)
        {
            _statuses.Post($"{RepositoryName()}: refreshed");
        }

        RunAll(applied);
        RunNext();
    }

    // The kinds that make the refresher read the status (see RepositoryRefresher).
    private static bool ReadsStatus(RepositoryChangeKinds kinds) => (kinds & StatusKinds) != RepositoryChangeKinds.None;

    private List<Action> TakeInFlight()
    {
        var taken = _inFlight;
        _inFlight = [];
        return taken;
    }

    private static void RunAll(List<Action> actions)
    {
        foreach (var action in actions)
        {
            action();
        }
    }

    private void Fail(Exception exception, RepositoryChangeKinds kinds, bool reloadGraph, bool isManual)
    {
        _isRunning = false;
        var applied = TakeInFlight();

        // The run cleared the pending kinds when it started, so anything pending now was requested while it ran (an
        // explicit refresh after an operation, say). That request needs its own run: nothing else would start one.
        var requestedMeanwhile = _pending != RepositoryChangeKinds.None;
        _pending |= kinds;
        _forceGraph |= reloadGraph;
        _announce |= isManual;
        if (_isDisposed)
        {
            return;
        }

        // A repository that keeps failing (polling every few seconds) is reported once, not on every tick.
        var message = $"{RepositoryName()}: could not refresh: {FailureText.Reason(exception)}";
        if (isManual || message != _lastError)
        {
            _lastError = message;
            _statuses.Error(message);
        }

        RunAll(applied);
        if (requestedMeanwhile)
        {
            RunNext();
        }
    }

    private string RepositoryName() => Path.GetFileName(Path.TrimEndingDirectorySeparator(_location.RootPath));
}
