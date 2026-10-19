using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>
/// The watcher set of one tab: file system watchers on the git directory, the common directory (when linked), refs, linked worktrees'
/// git directories and the working tree. Events are classified, merged and debounced; the callback receives what may have changed.
/// A background tab (<see cref="IsActive"/> false) does not call back: it keeps the pending changes and reports them when it is
/// activated. When the shared <see cref="WatchBudget"/> is exhausted, or a watcher cannot be created or fails, the tab polls instead.
/// <c>BecameStale</c> notifications go through an injectable queue (thread pool by default). The callback runs on a thread pool thread (on the caller's thread for the report made by <see cref="SetActive"/>) and must not throw.
/// </summary>
public sealed class RepositoryWatcher : IDisposable
{
    private const int MaximumNewSubdirectories = 64;

    private static readonly NotifyFilters GitDirectoryFilters = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size;

    private readonly RepositoryLocation _location;
    private readonly WatchBudget _budget;
    private readonly RepositoryWatcherOptions _options;
    private readonly Action<RepositoryChangeKinds> _onChanged;
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
    private int _lastFingerprint;
    private bool _isRescanning;
    private bool _rescanAgain;
    private bool _hasScanned;
    private bool _usesRecursiveRoot;

    public RepositoryWatcher(
        RepositoryLocation location,
        WatchBudget budget,
        RepositoryWatcherOptions options,
        Action<RepositoryChangeKinds> onChanged,
        Action<Action>? queueNotification = null,
        IWorkingTreeDirectoryLister? directoryLister = null)
    {
        _directoryLister = directoryLister;
        _location = location;
        _budget = budget;
        _options = options;
        _onChanged = onChanged;
        _queueNotification = queueNotification ?? (work => ThreadPool.QueueUserWorkItem(_ => work()));
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

        _onChanged(report);
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

        if (_directoryLister is not null && IsInWorkingTree(fullPath))
        {
            OnWorkingTreePathChanged(fullPath, isCreation);
        }
    }

    private bool IsInWorkingTree(string fullPath)
    {
        var root = _location.WorkingTreePath;
        if (root is null)
        {
            return false;
        }

        var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var gitDirectory = Path.TrimEndingDirectorySeparator(_location.GitDirectory) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, comparison)
            && !fullPath.StartsWith(gitDirectory, comparison);
    }

    /// <summary>
    /// A new directory is watched at once (it may be empty and so unknown to git); a vanished one triggers a rescan.
    /// </summary>
    private void OnWorkingTreePathChanged(string fullPath, bool isCreation)
    {
        if (isCreation && Directory.Exists(fullPath))
        {
            WatchNewDirectory(fullPath);
            RequestRescan();
            return;
        }

        bool isWatched;
        lock (_gate)
        {
            isWatched = _directoryWatchers.ContainsKey(fullPath);
        }

        if (isWatched && !Directory.Exists(fullPath))
        {
            RequestRescan();
        }
    }

    /// <summary>
    /// Watches a created directory and a few of its subdirectories (a quick <c>mkdir -p</c> or unpack); the rescan that
    /// follows adds what git lists and drops what it ignores, so a big new tree never fills the cap. The watchers are
    /// created outside the gate; only the bookkeeping happens under it.
    /// </summary>
    private void WatchNewDirectory(string path)
    {
        var children = ListNewSubdirectories(path);
        List<string> wanted;
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null || !_hasScanned)
            {
                return;
            }

            wanted =
            [
                .. new[] { path }.Concat(children).Where(candidate => !_directoryWatchers.ContainsKey(candidate)),
            ];
        }

        var created = CreateDirectoryWatchers(wanted, out var failed);
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null || !_hasScanned || failed)
            {
                if (failed && !_isDisposed)
                {
                    FallBackToPollingLocked();
                }

                RetireWatchers([.. created.Values]);
                return;
            }

            InstallLocked(created, stopAtLimit: true);
            EnqueueLocked(RepositoryChangeKinds.WorkingTree);
        }
    }

    private static List<string> ListNewSubdirectories(string path)
    {
        var children = new List<string>();
        try
        {
            foreach (var child in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
            {
                if (children.Count >= MaximumNewSubdirectories)
                {
                    break;
                }

                children.Add(child);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The directory went away again; the rescan tidies up.
        }

        return children;
    }

    /// <summary>
    /// Creates one non-recursive watcher per path without holding the gate (an inotify call each).
    /// A directory that vanished meanwhile is skipped; any other failure sets <paramref name="failed"/> and stops.
    /// </summary>
    private Dictionary<string, FileSystemWatcher> CreateDirectoryWatchers(IEnumerable<string> paths, out bool failed)
    {
        var created = new Dictionary<string, FileSystemWatcher>(StringComparer.Ordinal);
        failed = false;
        foreach (var path in paths)
        {
            if (_disposal.IsCancellationRequested)
            {
                break;
            }

            try
            {
                created[path] = CreateWatcher(path, isRecursive: false);
            }
            catch (Exception exception) when (IsWatchFailure(exception))
            {
                if (Directory.Exists(path))
                {
                    failed = true;
                    break;
                }
            }
            catch
            {
                // Unexpected: do not leak what was created so far (it is not tracked anywhere yet).
                RetireWatchers([.. created.Values]);
                throw;
            }
        }

        return created;
    }

    /// <summary>
    /// Takes over watchers created outside the gate. Duplicates (another path watched the directory first) are retired.
    /// At the per-tab cap, either stops quietly or falls back to polling.
    /// </summary>
    private int InstallLocked(Dictionary<string, FileSystemWatcher> created, bool stopAtLimit)
    {
        var added = 0;
        var retired = new List<FileSystemWatcher>();
        foreach (var (path, watcher) in created)
        {
            if (_directoryWatchers.ContainsKey(path))
            {
                retired.Add(watcher);
            }
            else if (_pollTimer is not null || _directoryWatchers.Count >= _options.MaximumWorkingTreeDirectories)
            {
                retired.Add(watcher);
                if (!stopAtLimit)
                {
                    FallBackToPollingLocked();
                }
            }
            else
            {
                _directoryWatchers[path] = watcher;
                added++;
            }
        }

        if (retired.Count > 0)
        {
            RetireWatchers([.. retired]);
        }

        return added;
    }

    private void RequestRescan()
    {
        lock (_gate)
        {
            if (_directoryLister is null || _isDisposed || _pollTimer is not null || _location.WorkingTreePath is null)
            {
                return;
            }

            if (_isRescanning)
            {
                _rescanAgain = true;
                return;
            }

            _isRescanning = true;
        }

        _ = Task.Run(RescanLoopAsync);
    }

    private async Task RescanLoopAsync()
    {
        try
        {
            var token = _disposal.Token;
            while (!token.IsCancellationRequested)
            {
                // Coalesces the burst of directory events a build or install causes into one listing.
                await Task.Delay(_options.Debounce, token);
                var directories = await _directoryLister!.ListAsync(_location.WorkingTreePath!, token);
                var drops = FindDroppableDirectories(directories);
                if (!ApplyDirectories(directories, drops))
                {
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // Disposed while listing.
        }
        finally
        {
            lock (_gate)
            {
                _isRescanning = false;
            }
        }
    }

    /// <summary>Checks the disk outside the lock: watched directories git does not list, gone or ignored.</summary>
    private List<string> FindDroppableDirectories(IReadOnlyList<string>? directories)
    {
        var drops = new List<string>();
        if (directories is null)
        {
            return drops;
        }

        string[] watched;
        lock (_gate)
        {
            watched = [.. _directoryWatchers.Keys];
        }

        var wanted = new HashSet<string>(directories, StringComparer.Ordinal);
        foreach (var path in watched)
        {
            if (!wanted.Contains(path) && ShouldDrop(path))
            {
                drops.Add(path);
            }
        }

        return drops;
    }

    /// <summary>
    /// Applies one listing in three steps so the gate is never held while watchers are created: plan under the gate,
    /// create outside it, install under it. False when the loop should end (disposed, polling or nothing queued).
    /// </summary>
    private bool ApplyDirectories(IReadOnlyList<string>? directories, List<string> drops)
    {
        var additions = PlanListing(directories, drops);
        var created = CreateDirectoryWatchers(additions, out var failed);
        lock (_gate)
        {
            if (_isDisposed)
            {
                RetireWatchers([.. created.Values]);
                return false;
            }

            if (_pollTimer is null)
            {
                if (failed)
                {
                    FallBackToPollingLocked();
                }
                else
                {
                    CompleteListingLocked(directories is not null, created);
                    created.Clear();
                }
            }

            if (created.Count > 0)
            {
                RetireWatchers([.. created.Values]);
            }

            if (!_rescanAgain || _pollTimer is not null)
            {
                _rescanAgain = false;
                _isRescanning = false;
                return false;
            }

            _rescanAgain = false;

            return true;
        }
    }

    /// <summary>Under the gate: handles drops; returns the directories still needing a watcher.</summary>
    private List<string> PlanListing(IReadOnlyList<string>? directories, List<string> drops)
    {
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null)
            {
                return [];
            }

            if (directories is null)
            {
                // A failed first listing falls back to the recursive watcher; a later failure keeps what is watched.
                if (!_hasScanned)
                {
                    WatchRootRecursivelyLocked();
                }

                return [];
            }

            if (directories.Count > _options.MaximumWorkingTreeDirectories)
            {
                FallBackToPollingLocked();
                return [];
            }

            foreach (var path in drops)
            {
                if (_directoryWatchers.ContainsKey(path))
                {
                    RetireDirectory(path);
                }
            }

            return [.. directories.Where(path => !_directoryWatchers.ContainsKey(path))];
        }
    }

    private void CompleteListingLocked(bool listed, Dictionary<string, FileSystemWatcher> created)
    {
        if (!listed)
        {
            return;
        }

        var added = InstallLocked(created, stopAtLimit: false);
        var hadScanned = _hasScanned;
        _hasScanned = true;
        if (hadScanned && added > 0 && _pollTimer is null)
        {
            // A file may have appeared in a directory before its watcher existed.
            EnqueueLocked(RepositoryChangeKinds.WorkingTree);
        }
    }

    private static bool IsWatchFailure(Exception exception) =>
        exception is IOException or ArgumentException or UnauthorizedAccessException
            or PlatformNotSupportedException or InvalidOperationException;

    /// <summary>
    /// A watched directory that git does not list is dropped when it is gone or holds files (ignored);
    /// an empty one stays.
    /// </summary>
    private static bool ShouldDrop(string path)
    {
        try
        {
            return !Directory.Exists(path)
                || Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private void RetireDirectory(string path)
    {
        var watcher = _directoryWatchers[path];
        _directoryWatchers.Remove(path);
        RetireWatchers([watcher]);
    }

    /// <summary>Git could not list the files: watch the whole tree recursively, as before.</summary>
    private void WatchRootRecursivelyLocked()
    {
        if (_usesRecursiveRoot || _location.WorkingTreePath is null)
        {
            return;
        }

        _usesRecursiveRoot = true;
        if (!_budget.TryAcquire(1))
        {
            FallBackToPollingLocked();
            return;
        }

        _heldSlots++;
        try
        {
            _watchers.Add(CreateWatcher(_location.WorkingTreePath, isRecursive: true));
        }
        catch (Exception exception) when (IsWatchFailure(exception))
        {
            FallBackToPollingLocked();
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
            catch (Exception exception) when (IsWatchFailure(exception))
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
        RetireWatchers([.. _watchers, .. _directoryWatchers.Values]);
        _watchers.Clear();
        _directoryWatchers.Clear();
        _watchedPaths.Clear();
        _budget.Release(_heldSlots);
        _heldSlots = 0;
    }

    private static void RetireWatchers(FileSystemWatcher[] retired) =>
        Task.Run(() =>
        {
            foreach (var watcher in retired)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        });

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
                _queueNotification(RaiseBecameStale);
            }

            return;
        }

        // Wait for quiet, but never longer than the maximum delay since the first event.
        var remaining = _options.MaximumDelay - (now - _pendingSince);
        var delay = remaining < _options.Debounce ? remaining : _options.Debounce;
        _debounceTimer.Change(delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
    }

    private void RaiseBecameStale() => BecameStale?.Invoke();

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
