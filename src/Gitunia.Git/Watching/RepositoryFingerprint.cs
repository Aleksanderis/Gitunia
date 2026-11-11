using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>
/// A cheap change detector for polling: hashes the name, size and time of the files that carry repository state
/// (HEAD, index, refs, worktree identity, operation files), one hash per group so a difference says what changed
/// (see <see cref="RepositoryFingerprintGroups.ChangedKinds"/>). Other worktrees' index and log files are left out, as the
/// watcher's <see cref="RepositoryChangeClassifier"/> does. Working tree edits are not covered; those need a status read.
/// </summary>
public static class RepositoryFingerprint
{
    /// <summary>The hashes of name, size and time of the files that carry repository state.</summary>
    public static RepositoryFingerprintGroups Compute(RepositoryLocation location)
    {
        var groups = new Accumulator();
        AddRootFiles(ref groups, location, location.GitDirectory);
        if (location.IsLinkedWorktree)
        {
            AddRootFiles(ref groups, location, location.CommonDirectory);
        }

        AddRefs(ref groups, Path.Combine(location.CommonDirectory, "refs"));
        AddWorktrees(ref groups, location, Path.Combine(location.CommonDirectory, "worktrees"));
        return groups.ToGroups();
    }

    private static void AddRootFiles(ref Accumulator groups, RepositoryLocation location, string directory)
    {
        try
        {
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
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
            foreach (var entry in tree.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                groups.Add(RepositoryChangeKinds.Refs, entry);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            groups.AddMarker(directory);
        }
    }

    /// <summary>Only the top level of each linked worktree's directory (HEAD, gitdir, locked); its index and logs change on every status.</summary>
    private static void AddWorktrees(ref Accumulator groups, RepositoryLocation location, string directory)
    {
        var own = Path.TrimEndingDirectorySeparator(location.GitDirectory);
        try
        {
            foreach (var worktree in new DirectoryInfo(directory).EnumerateDirectories())
            {
                if (string.Equals(Path.TrimEndingDirectorySeparator(worktree.FullName), own, StringComparison.OrdinalIgnoreCase))
                {
                    // This tab's own directory is covered as the git directory.
                    continue;
                }

                // The directory entry itself is not hashed: its time changes with every lock file git creates inside.
                foreach (var entry in worktree.EnumerateFiles())
                {
                    groups.Add(RepositoryChangeClassifier.Classify(location, entry.FullName), entry);
                }
            }
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
                default:
                    break;
            }
        }

        /// <summary>A directory that cannot be read: every group sees it, so the state is not mistaken for "no files".</summary>
        public void AddMarker(string directory)
        {
            _head.Add(directory);
            _refs.Add(directory);
            _index.Add(directory);
            _worktrees.Add(directory);
            _operation.Add(directory);
        }

        public readonly RepositoryFingerprintGroups ToGroups() =>
            new(_head.ToHashCode(), _refs.ToHashCode(), _index.ToHashCode(), _worktrees.ToHashCode(), _operation.ToHashCode());

        private static void AddEntry(ref HashCode hash, FileSystemInfo entry)
        {
            hash.Add(entry.FullName);
            hash.Add(entry.LastWriteTimeUtc.Ticks);
            hash.Add(entry is FileInfo file ? file.Length : -1);
        }
    }
}
