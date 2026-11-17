using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>
/// A cheap change detector for polling: hashes the name, size and time of the files that carry repository state
/// (HEAD, index, refs, worktree identity, operation files), one hash per group so a difference says what changed
/// (see <see cref="RepositoryFingerprintGroups.ChangedKinds"/>). Other worktrees' index and log files are left out,
/// as the watcher's <see cref="RepositoryChangeClassifier"/> does, and so is the main worktree's index for a linked
/// tab. Working tree edits are not covered; those need a status read.
/// </summary>
public static class RepositoryFingerprint
{
    private const string IndexFile = "index";

    private static readonly HashSet<string> WorktreeIdentityFiles = new(StringComparer.Ordinal)
    {
        "HEAD",
        "gitdir",
        "locked",
    };

    /// <summary>The hashes of name, size and time of the files that carry repository state.</summary>
    public static RepositoryFingerprintGroups Compute(RepositoryLocation location)
    {
        var groups = new Accumulator();
        AddRootFiles(ref groups, location, location.GitDirectory, false);
        if (location.IsLinkedWorktree)
        {
            AddRootFiles(ref groups, location, location.CommonDirectory, true);
        }

        AddRefs(ref groups, Path.Combine(location.CommonDirectory, "refs"));
        AddReftable(ref groups, Path.Combine(location.CommonDirectory, RepositoryChangeClassifier.ReftableDirectory));
        if (location.IsLinkedWorktree)
        {
            AddReftable(ref groups, Path.Combine(location.GitDirectory, RepositoryChangeClassifier.ReftableDirectory));
        }

        AddWorktrees(ref groups, location, Path.Combine(location.CommonDirectory, "worktrees"));
        return groups.ToGroups();
    }

    private static void AddRootFiles(
        ref Accumulator groups,
        RepositoryLocation location,
        string directory,
        bool isCommonOfLinked)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                // The main worktree's index changes on every status there; a linked tab does not show it.
                if (isCommonOfLinked && entry.Name == IndexFile)
                {
                    continue;
                }

                groups.Add(RepositoryChangeClassifier.Classify(location, entry.FullName), entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    private static void AddRefs(ref Accumulator groups, string directory)
    {
        try
        {
            var tree = new DirectoryInfo(directory);
            // Files only: a directory's own time changes with the lock files git creates in it (and lags on Windows).
            // A ref is a file.
            foreach (var entry in tree.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                groups.Add(RepositoryChangeKinds.Refs, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    /// <summary>
    /// A reftable repository's refs: the table files and <c>tables.list</c> (names, sizes, times). The directory entry
    /// itself is not enough, its time may lag on some filesystems. Absent in a files-backend repository.
    /// </summary>
    private static void AddReftable(ref Accumulator groups, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFiles())
            {
                groups.Add(RepositoryChangeKinds.Refs, entry);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Files backend.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    /// <summary>
    /// Only the identity files of each linked worktree's directory (HEAD, gitdir, locked); its index, logs and the
    /// files a commit or fetch there writes change too often.
    /// </summary>
    private static void AddWorktrees(ref Accumulator groups, RepositoryLocation location, string directory)
    {
        var own = Path.TrimEndingDirectorySeparator(location.GitDirectory);
        try
        {
            foreach (var worktree in new DirectoryInfo(directory).EnumerateDirectories())
            {
                var name = Path.TrimEndingDirectorySeparator(worktree.FullName);
                if (string.Equals(name, own, StringComparison.OrdinalIgnoreCase))
                {
                    // This tab's own directory is covered as the git directory.
                    continue;
                }

                // The directory entry itself is not hashed: its time changes with every lock file git creates inside.
                foreach (var entry in worktree.EnumerateFiles())
                {
                    if (WorktreeIdentityFiles.Contains(entry.Name))
                    {
                        groups.Add(RepositoryChangeKinds.Worktrees, entry);
                    }
                }

                // Reftable: the worktree's HEAD is a stub, its own refs live here.
                AddWorktreeReftable(ref groups, Path.Combine(worktree.FullName, RepositoryChangeClassifier.ReftableDirectory));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    private static void AddWorktreeReftable(ref Accumulator groups, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFiles())
            {
                groups.Add(RepositoryChangeKinds.Worktrees, entry);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Files backend.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    private struct Accumulator
    {
        private HashCode _head;
        private HashCode _refs;
        private HashCode _index;
        private HashCode _worktrees;
        private HashCode _operation;

        public void Add(RepositoryChangeKinds kind, FileSystemInfo entry)
        {
            if (entry.Name.EndsWith(".lock", StringComparison.Ordinal))
            {
                return;
            }

            switch (kind)
            {
                case RepositoryChangeKinds.Head:
                    AddEntry(ref _head, entry);
                    break;
                case RepositoryChangeKinds.Refs:
                    AddEntry(ref _refs, entry);
                    break;
                case RepositoryChangeKinds.Index:
                    AddEntry(ref _index, entry);
                    break;
                case RepositoryChangeKinds.Worktrees:
                    AddEntry(ref _worktrees, entry);
                    break;
                case RepositoryChangeKinds.Operation:
                    AddEntry(ref _operation, entry);
                    break;
            }
        }

        /// <summary>A directory that cannot be read: every group sees it, so it is not mistaken for "no files".</summary>
        public void AddMarker(string directory)
        {
            _head.Add(directory);
            _refs.Add(directory);
            _index.Add(directory);
            _worktrees.Add(directory);
            _operation.Add(directory);
        }

        public RepositoryFingerprintGroups ToGroups() => new(
            _head.ToHashCode(),
            _refs.ToHashCode(),
            _index.ToHashCode(),
            _worktrees.ToHashCode(),
            _operation.ToHashCode());

        private static void AddEntry(ref HashCode hash, FileSystemInfo entry)
        {
            hash.Add(entry.FullName);
            hash.Add(entry.LastWriteTimeUtc.Ticks);
            hash.Add(entry is FileInfo file ? file.Length : -1);
        }
    }
}
