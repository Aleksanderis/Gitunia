using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees.Models;

namespace Gitunia.Core.Worktrees;

/// <summary>An operation on worktrees. Each posts one result <see cref="WorktreeCommand"/>; none changes
/// state.</summary>
public abstract record WorktreeEffect : OperationEffect
{
    private protected WorktreeEffect(TabId tabId, RepositoryLocation location)
        : base(tabId, RunningOperationKind.Worktrees) => Location = location;

    /// <summary>Where the operation runs.</summary>
    public RepositoryLocation Location { get; }
}

/// <summary>Add a worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Request">The path, the branch and where a new branch starts.</param>
/// <param name="OpenAfter">True to open the new worktree as its own tab once it exists.</param>
public sealed record AddWorktreeEffect(
    TabId TabId,
    RepositoryLocation Location,
    WorktreeAddRequest Request,
    bool OpenAfter) : WorktreeEffect(TabId, Location);

/// <summary>Remove a worktree; without <paramref name="Force"/> one with uncommitted changes stays and the user is
/// asked.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Path">The worktree.</param>
/// <param name="Force">True when the user answered the question about the changes.</param>
public sealed record RemoveWorktreeEffect(TabId TabId, RepositoryLocation Location, string Path, bool Force)
    : WorktreeEffect(TabId, Location);

/// <summary>Lock a worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Path">The worktree.</param>
/// <param name="Reason">Why, empty for no reason.</param>
public sealed record LockWorktreeEffect(TabId TabId, RepositoryLocation Location, string Path, string Reason)
    : WorktreeEffect(TabId, Location);

/// <summary>Unlock a worktree.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
/// <param name="Path">The worktree.</param>
public sealed record UnlockWorktreeEffect(TabId TabId, RepositoryLocation Location, string Path)
    : WorktreeEffect(TabId, Location);

/// <summary>Delete the git records of worktrees whose directory is gone.</summary>
/// <param name="TabId">The tab.</param>
/// <param name="Location">Where the operation runs.</param>
public sealed record PruneWorktreesEffect(TabId TabId, RepositoryLocation Location) : WorktreeEffect(TabId, Location);

/// <summary>
/// Resolve the path of a worktree to its location and post <see cref="WorktreeLocated"/>. It only reads, so it is not
/// an operation of the tab: it does not mark the tab busy. A running operation blocks only the follow-up of a switch.
/// </summary>
/// <param name="TabId">The tab whose cancellation ends the read (the tab was closed).</param>
/// <param name="Path">The worktree directory.</param>
/// <param name="IsSwitch">True to point the tab at it, false to open it as its own tab.</param>
public sealed record LocateWorktreeEffect(TabId TabId, string Path, bool IsSwitch) : AppEffect;
