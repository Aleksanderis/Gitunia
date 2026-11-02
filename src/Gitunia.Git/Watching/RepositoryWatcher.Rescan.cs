using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

public sealed partial class RepositoryWatcher
{
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
}
