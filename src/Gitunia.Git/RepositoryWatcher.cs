namespace Gitunia.Git;

/// <summary>
/// The watcher set of one tab: file system watchers on the git directory, the common directory (when linked), refs, linked worktrees'
/// git directories and the working tree. Events are classified, merged and debounced; the callback receives what may have changed.
/// A background tab (<see cref="IsActive"/> false) does not call back: it keeps the pending changes and reports them when it is
/// activated. When the shared <see cref="WatchBudget"/> is exhausted, or a watcher cannot be created or fails, the tab polls instead.
/// The callback runs on a thread pool thread (on the caller's thread for the report made by <see cref="SetActive"/>) and must not throw.
/// </summary>
public sealed class RepositoryWatcher : IDisposable
{
    private static readonly NotifyFilters GitDirectoryFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;

    private readonly RepositoryLocation _location;
    private readonly WatchBudget _budget;
    private readonly RepositoryWatcherOptions _options;
    private readonly Action<RepositoryChangeKinds> _onChanged;
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly HashSet<string> _watchedPaths = new(StringComparer.Ordinal);
    private readonly Timer _debounceTimer;
    private Timer? _pollTimer;
    private RepositoryChangeKinds _pending;
    private DateTime _pendingSince;
    private int _heldSlots;
    private bool _isActive = true;
    private bool _isStarted;
    private bool _isDisposed;
    private int _lastFingerprint;

    public RepositoryWatcher(
        RepositoryLocation location,
        WatchBudget budget,
        RepositoryWatcherOptions options,
        Action<RepositoryChangeKinds> onChanged)
    {
        _location = location;
        _budget = budget;
        _options = options;
        _onChanged = onChanged;
        _debounceTimer = new Timer(_ => OnDebounceElapsed());
    }

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
    /// need not poll <see cref="IsStale"/>. Not raised again until the changes were reported.
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
            }
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
            if (!isActive || _pending == RepositoryChangeKinds.None)
            {
                return;
            }

            report = _pending;
            _pending = RepositoryChangeKinds.None;
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        _onChanged(report);
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
            ReleaseWatchers();
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        _debounceTimer.Dispose();
    }

    private List<(string Path, bool IsRecursive)> CreatePlan()
    {
        var plan = new List<(string Path, bool IsRecursive)>();
        AddIfExists(plan, _location.GitDirectory, false);
        if (_location.IsLinkedWorktree)
        {
            AddIfExists(plan, _location.CommonDirectory, false);
        }

        AddIfExists(plan, Path.Combine(_location.CommonDirectory, "refs"), true);
        AddIfExists(plan, Path.Combine(_location.CommonDirectory, "worktrees"), true);
        if (_location.WorkingTreePath is not null)
        {
            AddIfExists(plan, _location.WorkingTreePath, true);
        }

        return plan;
    }

    private static void AddIfExists(List<(string Path, bool IsRecursive)> plan, string path, bool isRecursive)
    {
        if (Directory.Exists(path))
        {
            plan.Add((path, isRecursive));
        }
    }

    private FileSystemWatcher CreateWatcher(string path, bool isRecursive)
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

    private void OnPathChanged(string fullPath, bool isCreation)
    {
        if (isCreation && IsWorktreesDirectory(fullPath))
        {
            WatchWorktreesDirectory(fullPath);
        }

        var kinds = RepositoryChangeClassifier.Classify(_location, fullPath);
        if (kinds != RepositoryChangeKinds.None)
        {
            Enqueue(kinds);
        }
    }

    private bool IsWorktreesDirectory(string fullPath) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(fullPath),
            Path.Combine(Path.TrimEndingDirectorySeparator(_location.CommonDirectory), "worktrees"),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>The first <c>git worktree add</c> creates the directory after we started; start watching it.</summary>
    private void WatchWorktreesDirectory(string path)
    {
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null || _watchedPaths.Contains(path))
            {
                return;
            }

            if (!_budget.TryAcquire(1))
            {
                FallBackToPollingLocked();
                return;
            }

            _heldSlots++;
            try
            {
                _watchers.Add(CreateWatcher(path, isRecursive: true));
                _watchedPaths.Add(path);
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException or InvalidOperationException)
            {
                FallBackToPollingLocked();
            }
        }
    }

    private void FallBackToPolling()
    {
        lock (_gate)
        {
            FallBackToPollingLocked();
        }
    }

    private void FallBackToPollingLocked()
    {
        if (_isDisposed || _pollTimer is not null)
        {
            return;
        }

        StartPolling();
        // Anything may have been missed while the watchers were failing.
        EnqueueLocked(RepositoryChangeKinds.All);
    }

    private void StartPolling()
    {
        ReleaseWatchers();
        _lastFingerprint = ComputeFingerprint();
        _pollTimer = new Timer(_ => Poll(), null, _options.PollInterval, _options.PollInterval);
    }

    private void ReleaseWatchers()
    {
        // Disposing can wait for in-flight handlers, which take the gate, so it happens off this thread.
        var retired = _watchers.ToArray();
        Task.Run(() =>
        {
            foreach (var watcher in retired)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        });
        _watchers.Clear();
        _watchedPaths.Clear();
        _budget.Release(_heldSlots);
        _heldSlots = 0;
    }

    private void Poll()
    {
        var fingerprint = ComputeFingerprint();
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            // Working tree edits cannot be seen cheaply, so a polling tab asks for a status refresh on every tick;
            // the refresher drops it when nothing differs.
            var kinds = RepositoryChangeKinds.WorkingTree;
            if (fingerprint != _lastFingerprint)
            {
                _lastFingerprint = fingerprint;
                kinds |= RepositoryChangeKinds.All;
            }

            EnqueueLocked(kinds);
        }
    }

    /// <summary>A hash of name, size and time of the files that carry repository state.</summary>
    private int ComputeFingerprint()
    {
        var hash = new HashCode();
        AddRootFiles(ref hash, _location.GitDirectory);
        if (_location.IsLinkedWorktree)
        {
            AddRootFiles(ref hash, _location.CommonDirectory);
        }

        AddTree(ref hash, Path.Combine(_location.CommonDirectory, "refs"));
        AddTree(ref hash, Path.Combine(_location.CommonDirectory, "worktrees"));
        return hash.ToHashCode();
    }

    private static void AddRootFiles(ref HashCode hash, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                AddEntry(ref hash, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash.Add(directory);
        }
    }

    private static void AddTree(ref HashCode hash, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                AddEntry(ref hash, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            hash.Add(directory);
        }
    }

    private static void AddEntry(ref HashCode hash, FileSystemInfo entry)
    {
        if (entry.Name.EndsWith(".lock", StringComparison.Ordinal))
        {
            return;
        }

        hash.Add(entry.FullName);
        hash.Add(entry.LastWriteTimeUtc.Ticks);
        hash.Add(entry is FileInfo file ? file.Length : -1);
    }

    private void Enqueue(RepositoryChangeKinds kinds)
    {
        lock (_gate)
        {
            EnqueueLocked(kinds);
        }
    }

    private void EnqueueLocked(RepositoryChangeKinds kinds)
    {
        if (_isDisposed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_pending == RepositoryChangeKinds.None)
        {
            _pendingSince = now;
        }

        var wasPending = _pending != RepositoryChangeKinds.None;
        _pending |= kinds;
        if (!_isActive)
        {
            if (!wasPending)
            {
                ThreadPool.QueueUserWorkItem(_ => BecameStale?.Invoke());
            }

            return;
        }

        // Wait for quiet, but never longer than the maximum delay since the first event.
        var remaining = _options.MaximumDelay - (now - _pendingSince);
        var delay = remaining < _options.Debounce ? remaining : _options.Debounce;
        _debounceTimer.Change(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
    }

    private void OnDebounceElapsed()
    {
        RepositoryChangeKinds report;
        lock (_gate)
        {
            if (_isDisposed || !_isActive || _pending == RepositoryChangeKinds.None)
            {
                return;
            }

            report = _pending;
            _pending = RepositoryChangeKinds.None;
        }

        _onChanged(report);
    }
}
