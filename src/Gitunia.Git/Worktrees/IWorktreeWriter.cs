using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees.Models;

namespace Gitunia.Git.Worktrees;

/// <summary>
/// Adds, removes, locks, unlocks and prunes linked worktrees. The seam the worktree actions use so tests can replace git; callers
/// confirm before <see cref="RemoveAsync"/> and <see cref="PruneAsync"/>. Every call is scoped to the repository of
/// <c>location</c>, never to the process working directory.
/// </summary>
public interface IWorktreeWriter
{
    /// <summary>
    /// Creates a worktree (<c>git worktree add</c>), with a new branch when the request has a start point. Git's refusals (a branch
    /// held by another worktree, a path that exists and is not empty, an invalid branch name) come back as a
    /// <see cref="Execution.Exceptions.GitCommandFailedException"/>.
    /// </summary>
    Task AddAsync(RepositoryLocation location, WorktreeAddRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Lists what keeps a worktree from being removed without <c>force</c>: its modified, staged and untracked paths (ignored files
    /// do not count, as for <c>git worktree remove</c>). Empty for a clean worktree.
    /// </summary>
    Task<IReadOnlyList<string>> ListChangesAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a linked worktree (<c>git worktree remove</c>); the branch it had checked out is kept. Without <paramref name="force"/>
    /// git refuses a dirty or locked worktree; with it uncommitted changes are lost for good (a lock is never overridden).
    /// </summary>
    Task RemoveAsync(RepositoryLocation location, string path, bool force, CancellationToken cancellationToken);

    /// <summary>Locks a worktree against pruning and removal, with an optional reason.</summary>
    Task LockAsync(RepositoryLocation location, string path, string? reason, CancellationToken cancellationToken);

    /// <summary>Unlocks a worktree.</summary>
    Task UnlockAsync(RepositoryLocation location, string path, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the administrative data of worktrees whose directory is gone (<c>git worktree prune</c>) and returns what
    /// git reported removing, one line per entry.
    /// </summary>
    Task<IReadOnlyList<string>> PruneAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
