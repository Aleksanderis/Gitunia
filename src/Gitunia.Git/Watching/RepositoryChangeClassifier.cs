using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Watching;

/// <summary>Maps a changed path to what it may have changed in the repository. Pure, so it is tested without watching anything.</summary>
public static class RepositoryChangeClassifier
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Where a reftable repository (<c>--ref-format=reftable</c>) keeps its refs; <c>HEAD</c> and <c>refs/heads</c> are stubs there.</summary>
    public const string ReftableDirectory = "reftable";

    /// <summary>A reftable table or <c>tables.list</c> change may move HEAD and any ref.</summary>
    private const RepositoryChangeKinds ReftableKinds = RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs;

    private static readonly HashSet<string> OperationEntries = new(StringComparer.Ordinal)
    {
        "MERGE_HEAD",
        "CHERRY_PICK_HEAD",
        "REVERT_HEAD",
        "BISECT_LOG",
        "BISECT_START",
        "rebase-merge",
        "rebase-apply",
        "sequencer",
    };

    public static RepositoryChangeKinds Classify(RepositoryLocation location, string fullPath)
    {
        // The own git directory first: a linked worktree's lives inside the common directory.
        if (TryGetRelative(location.GitDirectory, fullPath, out var ownRelative))
        {
            return IsLockFile(fullPath) ? RepositoryChangeKinds.None : ClassifyOwnGitDirectory(ownRelative);
        }

        if (TryGetRelative(location.CommonDirectory, fullPath, out var commonRelative))
        {
            return IsLockFile(fullPath) ? RepositoryChangeKinds.None : ClassifyCommonDirectory(commonRelative, location.IsLinkedWorktree);
        }

        if (location.WorkingTreePath is not null && TryGetRelative(location.WorkingTreePath, fullPath, out _))
        {
            return RepositoryChangeKinds.WorkingTree;
        }

        return RepositoryChangeKinds.None;
    }

    /// <summary>Git's transient lock files; only meaningful inside git directories (a tracked Cargo.lock is a normal file).</summary>
    private static bool IsLockFile(string fullPath) => fullPath.EndsWith(".lock", StringComparison.Ordinal);

    private static RepositoryChangeKinds ClassifyOwnGitDirectory(string relative)
    {
        var first = FirstSegment(relative);
        return first switch
        {
            "HEAD" => RepositoryChangeKinds.Head,
            "index" => RepositoryChangeKinds.Index,
            "refs" or "packed-refs" or "config" => RepositoryChangeKinds.Refs,
            ReftableDirectory => ReftableKinds,
            "worktrees" => ClassifyLinkedWorktreeEntry(relative),
            _ when OperationEntries.Contains(first) => RepositoryChangeKinds.Operation,
            _ => RepositoryChangeKinds.None,
        };
    }

    private static RepositoryChangeKinds ClassifyCommonDirectory(string relative, bool isLinkedWorktree)
    {
        var first = FirstSegment(relative);
        if (first is "refs" or "packed-refs" or "config")
        {
            return RepositoryChangeKinds.Refs;
        }

        if (first == ReftableDirectory)
        {
            return ReftableKinds;
        }

        if (first == "worktrees")
        {
            return ClassifyLinkedWorktreeEntry(relative);
        }

        // A linked worktree's tab does not show the main worktree's HEAD or index, but the worktree list does.
        return isLinkedWorktree && (first == "HEAD" || first == "index") ? RepositoryChangeKinds.Worktrees : RepositoryChangeKinds.None;
    }

    /// <summary>Other worktrees' index files change on every status; only their identity files matter.</summary>
    private static RepositoryChangeKinds ClassifyLinkedWorktreeEntry(string relative) =>
        Path.GetFileName(relative) == "index" ? RepositoryChangeKinds.None : RepositoryChangeKinds.Worktrees;

    private static string FirstSegment(string relative)
    {
        var end = relative.IndexOfAny(['/', '\\']);
        return end < 0 ? relative : relative[..end];
    }

    private static bool TryGetRelative(string root, string fullPath, out string relative)
    {
        relative = string.Empty;
        var trimmedRoot = Path.TrimEndingDirectorySeparator(root);
        if (fullPath.Length <= trimmedRoot.Length
            || !fullPath.StartsWith(trimmedRoot, PathComparison)
            || (fullPath[trimmedRoot.Length] != Path.DirectorySeparatorChar && fullPath[trimmedRoot.Length] != Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        relative = fullPath[(trimmedRoot.Length + 1)..];
        return true;
    }
}
