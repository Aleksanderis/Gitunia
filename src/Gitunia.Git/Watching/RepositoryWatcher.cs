using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>
/// The watcher set of one tab: file system watchers on the git directory, the common directory (when linked), refs, linked worktrees'
/// git directories and the working tree. Events are classified, merged and debounced; <see cref="Changed"/> reports what may have changed.
/// A background tab (<see cref="IsActive"/> false) does not raise <see cref="Changed"/>: it keeps the pending changes and reports them when it is
/// activated. When the shared <see cref="WatchBudget"/> is exhausted, or a watcher cannot be created or fails, the tab polls instead.
/// <c>BecameStale</c> notifications go through an injectable queue (thread pool by default).
/// </summary>
public sealed partial class RepositoryWatcher : IDisposable
{
    private const int MaximumNewSubdirectories = 64;
    private const int RelistPasses = 2;

    // A Windows watcher holds its buffer in non-paged kernel memory. The working tree sees build output, so it keeps the
    // maximum; git directories see a few small files at a time.
    private const int WorkingTreeBufferSize = 64 * 1024;
    private const int GitDirectoryBufferSize = 16 * 1024;

    private static readonly NotifyFilters GitDirectoryFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;

    private readonly RepositoryLocation _location;
    private readonly WatchBudget _budget;
    private readonly RepositoryWatcherOptions _options;
    private readonly Action<Action> _queueNotification;
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _watchedPaths = new(StringComparer.Ordinal);
    private readonly Timer _debounceTimer;
    private readonly IWorkingTreeDirectoryLister? _directoryLister;
    private readonly Dictionary<string, FileSystemWatcher> _directoryWatchers = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _disposal = new();
    private Timer? _pollTimer;
    private TimeSpan _pollInterval;
    private int _pollFailures;
    private bool _awaitsRefresh;
    private bool _isPolling;
    private RepositoryChangeKinds _pending;
    private DateTime _pendingSince;
    private int _heldSlots;
    private bool _isActive = true;
    private bool _isStarted;
    private bool _isDisposed;
    private RepositoryFingerprintGroups _lastFingerprint;
    private bool _isRescanning;
    private bool _rescanAgain;
    private bool _hasScanned;
    private bool _usesRecursiveRoot;
    private readonly bool _perDirectory;
    private readonly IRepositoryFingerprintReader _fingerprintReader;
    private bool _hasBaseline;
    private Task<RepositoryFingerprintGroups>? _baseline;
    private Timer? _retryTimer;
    private volatile HashSet<string>? _ignoredDirectories;

    public RepositoryWatcher(
        RepositoryLocation location,
        WatchBudget budget,
        RepositoryWatcherOptions options,
        Action<Action>? queueNotification = null,
        IWorkingTreeDirectoryLister? directoryLister = null,
        IRepositoryFingerprintReader? fingerprintReader = null)
    {
        _fingerprintReader = fingerprintReader ?? new DiskFingerprintReader();
        _directoryLister = directoryLister;
        _perDirectory = directoryLister is not null && options.UsesPerDirectoryWatching;
        _location = location;
        _budget = budget;
        _options = options;
        _queueNotification = queueNotification ?? (work => ThreadPool.QueueUserWorkItem(_ => work()));
        _debounceTimer = new Timer(_ => OnDebounceElapsed());
    }

    /// <summary>
    /// Raised with what may have changed (merged and debounced). Subscribe before <see cref="Start"/>: changes found
    /// before the first subscriber are not replayed. Runs on a thread pool thread (on the caller's thread for the
    /// report made by <see cref="SetActive"/>); handlers must not throw.
    /// </summary>
    public event Action<RepositoryChangeKinds>? Changed;

    /// <summary>The tab is visible: changes are reported as they happen.</summary>
    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _isActive;
            }
        }
    }

    /// <summary>The tab is in the background and changes are waiting to be reported on activation.</summary>
    public bool IsStale
    {
        get
        {
            lock (_gate)
            {
                return !_isActive && _pending != RepositoryChangeKinds.None;
            }
        }
    }

    /// <summary>
    /// Raised on a thread pool thread when a background tab turns stale (its first unreported change arrives), so callers and tests
    /// need not poll <see cref="IsStale"/>. Also raised when a tab with unreported changes is hidden. Not raised again until the changes were reported. Handlers must not throw.
    /// </summary>
    public event Action? BecameStale;

    /// <summary>The tab polls because no watcher slots were left or watching failed.</summary>
    public bool IsPolling
    {
        get
        {
            lock (_gate)
            {
                return _pollTimer is not null;
            }
        }
    }

    /// <summary>Number of ignored directories whose events the recursive working tree watcher drops (zero until git listed them).</summary>
    public int IgnoredDirectoryCount => _ignoredDirectories?.Count ?? 0;

    /// <summary>Number of file system watchers this tab currently holds.</summary>
    public int WatcherCount
    {
        get
        {
            lock (_gate)
            {
                return _watchers.Count;
            }
        }
    }

    /// <summary>
    /// Number of non-ignored working tree directories watched one by one
    /// (zero when the working tree is watched recursively).
    /// </summary>
    public int WorkingTreeDirectoryCount
    {
        get
        {
            lock (_gate)
            {
                return _directoryWatchers.Count;
            }
        }
    }

    /// <summary>
    /// Starts watching on a thread pool thread: creating the watchers and planning them touches the disk and can be slow on a network
    /// path, so the UI thread never does it. Completes when the watchers exist (or polling took over); a watcher disposed meanwhile
    /// is not an error.
    /// </summary>
    public Task StartAsync() => Task.Run(() =>
    {
        try
        {
            Start();
        }
        catch (ObjectDisposedException)
        {
            // Disposed before the pool thread got to it.
        }
    });

    /// <summary>Starts watching on the calling thread (tests and callers that are already off the UI thread; the UI uses <see cref="StartAsync"/>).</summary>
    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_isStarted)
            {
                return;
            }

            _isStarted = true;
            if (!TryStartWatchingLocked())
            {
                StartPolling();
            }
        }
    }

    /// <summary>Creates the watchers of the plan. False when polling is needed instead (no slots, or a watcher could not be created).</summary>
    private bool TryStartWatchingLocked()
    {
        var plan = CreatePlan();
        if (!_budget.TryAcquire(plan.Count))
        {
            return false;
        }

        _heldSlots = plan.Count;
        try
        {
            foreach (var entry in plan)
            {
                _watchers.Add(CreateWatcher(entry.Path, entry.IsRecursive));
                _watchedPaths.Add(entry.Path);
            }
        }
        catch (Exception exception) when (IsWatchFailure(exception))
        {
            // Typically the inotify watch limit: the caller gives the slots back and polls.
            ReleaseWatchers();
            return false;
        }

        if (!_perDirectory && _directoryLister is not null && _location.WorkingTreePath is not null)
        {
            _ = Task.Run(LoadIgnoredDirectoriesAsync);
        }

        RequestRescan();
        return true;
    }

    /// <summary>Marks the tab visible or hidden. Activating reports everything that changed while it was hidden.</summary>
    public void SetActive(bool isActive)
    {
        RepositoryChangeKinds report;
        lock (_gate)
        {
            if (_isDisposed || _isActive == isActive)
            {
                return;
            }

            _isActive = isActive;
            if (!isActive && _pending != RepositoryChangeKinds.None)
            {
                _queueNotification(RaiseBecameStale);
            }

            if (isActive)
            {
                // Hidden tabs skip their polls: look again at once.
                // A poll that is running re-arms the timer itself.
                _awaitsRefresh = false;
                if (!_isPolling)
                {
                    _pollTimer?.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
                }
            }

            if (!isActive || _pending == RepositoryChangeKinds.None)
            {
                return;
            }

            report = _pending;
            _pending = RepositoryChangeKinds.None;
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        Changed?.Invoke(report);
    }

    /// <summary>
    /// Feeds a change into the same path as a file system event (merge, debounce, stale handling). For callers that know something
    /// changed without a file event, and for tests that need a deterministic trigger.
    /// </summary>
    public void ReportChange(RepositoryChangeKinds kinds)
    {
        if (kinds != RepositoryChangeKinds.None)
        {
            Enqueue(kinds);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _disposal.Cancel();
            ReleaseWatchers();
            _pollTimer?.Dispose();
            _pollTimer = null;
            _retryTimer?.Dispose();
            _retryTimer = null;
        }

        _debounceTimer.Dispose();
        _disposal.Dispose();
    }

    private List<WatchTarget> CreatePlan()
    {
        var plan = new List<WatchTarget>();
        AddIfExists(plan, _location.GitDirectory, false);
        if (_location.IsLinkedWorktree)
        {
            AddIfExists(plan, _location.CommonDirectory, false);
        }

        AddIfExists(plan, Path.Combine(_location.CommonDirectory, "refs"), true);
        AddIfExists(plan, Path.Combine(_location.CommonDirectory, "worktrees"), true);

        // Reftable repositories write ref updates here (a flat directory); the git directory watcher is not recursive.
        AddIfExists(plan, Path.Combine(_location.CommonDirectory, RepositoryChangeClassifier.ReftableDirectory), false);
        if (_location.IsLinkedWorktree)
        {
            AddIfExists(plan, Path.Combine(_location.GitDirectory, RepositoryChangeClassifier.ReftableDirectory), false);
        }

        if (_location.WorkingTreePath is not null)
        {
            AddIfExists(plan, _location.WorkingTreePath, !_perDirectory);
        }

        return plan;
    }

    private static void AddIfExists(List<WatchTarget> plan, string path, bool isRecursive)
    {
        if (Directory.Exists(path))
        {
            plan.Add(new WatchTarget(path, isRecursive));
        }
    }

    private FileSystemWatcher CreateWatcher(string path, bool isRecursive)
    {
        var watcher = BuildWatcher(path, isRecursive);
        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }

        return watcher;
    }

    private FileSystemWatcher BuildWatcher(string path, bool isRecursive)
    {
        var isWorkingTreeRoot = isRecursive && IsWorkingTreeRoot(path);
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = isRecursive,
            NotifyFilter = GitDirectoryFilters,
            InternalBufferSize = isWorkingTreeRoot ? WorkingTreeBufferSize : GitDirectoryBufferSize,
        };

        watcher.Created += (_, arguments) => HandleEvent(arguments.FullPath, isCreation: true, isWorkingTreeRoot);
        watcher.Changed += (_, arguments) => HandleEvent(arguments.FullPath, isCreation: false, isWorkingTreeRoot);
        watcher.Deleted += (_, arguments) => HandleEvent(arguments.FullPath, isCreation: false, isWorkingTreeRoot);
        watcher.Renamed += (_, arguments) =>
        {
            HandleEvent(arguments.OldFullPath, isCreation: false, isWorkingTreeRoot);
            HandleEvent(arguments.FullPath, isCreation: true, isWorkingTreeRoot);
        };
        watcher.Error += (_, arguments) => OnWatcherError(arguments.GetException(), isWorkingTreeRoot);

        return watcher;
    }

    private void HandleEvent(string fullPath, bool isCreation, bool isWorkingTreeRoot)
    {
        if (!isWorkingTreeRoot || !IsDroppedInWorkingTreeRoot(fullPath))
        {
            OnPathChanged(fullPath, isCreation);
        }
    }

    private bool IsWorkingTreeRoot(string path) =>
        _location.WorkingTreePath is { } root
        && string.Equals(
            Path.TrimEndingDirectorySeparator(path),
            Path.TrimEndingDirectorySeparator(root),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Events the recursive working tree watcher drops: the git directory inside the tree (its own watchers cover what
    /// matters) and anything under an ignored directory (build output, dependencies).
    /// </summary>
    private bool IsDroppedInWorkingTreeRoot(string fullPath)
    {
        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var gitDirectory = Path.TrimEndingDirectorySeparator(_location.GitDirectory);
        if (fullPath.StartsWith(gitDirectory, comparison)
            && (fullPath.Length == gitDirectory.Length || IsSeparator(fullPath[gitDirectory.Length])))
        {
            return true;
        }

        var ignored = _ignoredDirectories;
        if (ignored is null || ignored.Count == 0)
        {
            return false;
        }

        // The ignored directory itself (created or deleted) counts too.
        var current = fullPath;
        while (!string.IsNullOrEmpty(current))
        {
            if (ignored.Contains(current))
            {
                return true;
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    private static bool IsSeparator(char value) =>
        value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;

    private async Task LoadIgnoredDirectoriesAsync()
    {
        try
        {
            var directories = await _directoryLister!.ListIgnoredAsync(_location.WorkingTreePath!, _disposal.Token);
            if (directories is not null)
            {
                var comparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
                _ignoredDirectories = new HashSet<string>(directories.Select(Path.TrimEndingDirectorySeparator), comparer);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // Disposed while listing.
        }
    }

    /// <summary>Feeds an error into the path a watcher's <c>Error</c> event takes (for tests, which cannot raise the event).</summary>
    public void ReportWatcherError(Exception exception, bool fromWorkingTree = false) =>
        OnWatcherError(exception, fromWorkingTree);

    /// <summary>
    /// A buffer overflow only means events were lost (routine during an install or a build): report everything once and keep
    /// watching. Any other error ends watching; the tab polls and tries to watch again later.
    /// </summary>
    private void OnWatcherError(Exception? exception, bool fromWorkingTree)
    {
        if (exception is InternalBufferOverflowException)
        {
            // The working tree overflows during a build: asking for more than its own kind would re-read refs every 2 s.
            Enqueue(fromWorkingTree ? RepositoryChangeKinds.WorkingTree : RepositoryChangeKinds.All);
            RequestRescan();
            return;
        }

        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null)
            {
                return;
            }

            FallBackToPollingLocked();
            _retryTimer ??= new Timer(
                _ => RetryWatching(),
                null,
                _options.WatcherRetryIntervalOrDefault,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The watchers failed earlier: try to watch again; keep polling when that fails too.</summary>
    private void RetryWatching()
    {
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is null)
            {
                return;
            }

            _pollTimer.Dispose();
            _pollTimer = null;
            _usesRecursiveRoot = false;
            _hasScanned = false;
            if (TryStartWatchingLocked())
            {
                _retryTimer?.Dispose();
                _retryTimer = null;
                EnqueueLocked(RepositoryChangeKinds.All);
                return;
            }

            StartPolling();
            _retryTimer?.Change(_options.WatcherRetryIntervalOrDefault, Timeout.InfiniteTimeSpan);
        }
    }
}
