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

    public RepositoryWatcher(
        RepositoryLocation location,
        WatchBudget budget,
        RepositoryWatcherOptions options,
        Action<Action>? queueNotification = null,
        IWorkingTreeDirectoryLister? directoryLister = null)
    {
        _directoryLister = directoryLister;
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
            var plan = CreatePlan();
            if (!_budget.TryAcquire(plan.Count))
            {
                StartPolling();
                return;
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
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
            {
                // Typically the inotify watch limit: give the slots back and poll.
                StartPolling();
                return;
            }

            RequestRescan();
        }
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
        if (_location.WorkingTreePath is not null)
        {
            AddIfExists(plan, _location.WorkingTreePath, _directoryLister is null);
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
        var watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = isRecursive,
            NotifyFilter = GitDirectoryFilters,
            InternalBufferSize = 64 * 1024,
        };
        watcher.Created += (_, arguments) => OnPathChanged(arguments.FullPath, isCreation: true);
        watcher.Changed += (_, arguments) => OnPathChanged(arguments.FullPath, isCreation: false);
        watcher.Deleted += (_, arguments) => OnPathChanged(arguments.FullPath, isCreation: false);
        watcher.Renamed += (_, arguments) =>
        {
            OnPathChanged(arguments.OldFullPath, isCreation: false);
            OnPathChanged(arguments.FullPath, isCreation: true);
        };
        watcher.Error += (_, _) => FallBackToPolling();

        return watcher;
    }
}
