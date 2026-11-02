using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

public sealed partial class RepositoryWatcher
{
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
    /// created outside the gate; only the bookkeeping happens under it. A subdirectory created after the first listing
    /// but before the new watchers were enabled raises no event anyone sees, so the listing is repeated a
    /// few times once the watchers are live. That narrows the window to almost nothing; the rescan covers the rest.
    /// </summary>
    private void WatchNewDirectory(string path)
    {
        if (!WatchDirectories(path, includeSelf: true))
        {
            return;
        }

        for (var pass = 0; pass < RelistPasses; pass++)
        {
            if (!WatchDirectories(path, includeSelf: false))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Installs watchers for the unwatched new subdirectories of <paramref name="path"/> (and the directory itself with
    /// <paramref name="includeSelf"/>). Returns false when watching stopped (disposed, polling, failure).
    /// </summary>
    private bool WatchDirectories(string path, bool includeSelf)
    {
        var children = ListNewSubdirectories(path);
        List<string> wanted;
        lock (_gate)
        {
            if (_isDisposed || _pollTimer is not null || !_hasScanned)
            {
                return false;
            }

            var candidates = includeSelf ? new[] { path }.Concat(children) : children;
            wanted = [.. candidates.Where(candidate => !_directoryWatchers.ContainsKey(candidate))];
        }

        if (wanted.Count == 0)
        {
            return true;
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
                return false;
            }

            InstallLocked(created, stopAtLimit: true);
            EnqueueLocked(RepositoryChangeKinds.WorkingTree);
        }

        return true;
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
}
