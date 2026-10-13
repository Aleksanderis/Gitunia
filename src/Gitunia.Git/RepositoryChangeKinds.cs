namespace Gitunia.Git;

/// <summary>What a file system event says may have changed in a repository. These are causes, not results: see <see cref="RepositorySections"/> for what a refresh found different.</summary>
[Flags]
public enum RepositoryChangeKinds
{
    None = 0,

    /// <summary>HEAD of this worktree moved or was re-pointed.</summary>
    Head = 1,

    /// <summary>Branches, tags, packed refs or config changed.</summary>
    Refs = 2,

    /// <summary>The index changed (stage, unstage, commit).</summary>
    Index = 4,

    /// <summary>A file in the working tree changed.</summary>
    WorkingTree = 8,

    /// <summary>The set or state of linked worktrees changed.</summary>
    Worktrees = 16,

    /// <summary>A merge, rebase, cherry-pick, revert or bisect started, advanced or ended.</summary>
    Operation = 32,

    All = Head | Refs | Index | WorkingTree | Worktrees | Operation,
}
