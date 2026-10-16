using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Branches;

/// <summary>
/// Creates, switches, renames and deletes local branches. Every call fails with the typed git exception when git refuses (a dirty
/// tree that conflicts, a branch checked out in another worktree, an unmerged branch); nothing is stashed or forced behind the
/// caller's back. This is the seam the branch actions use so tests can replace git.
/// </summary>
public interface IBranchWriter
{
    /// <summary>True when <paramref name="name"/> is a valid branch name (<c>git check-ref-format --branch</c>).</summary>
    Task<bool> IsValidNameAsync(RepositoryLocation location, string name, CancellationToken cancellationToken);

    /// <summary>Creates <paramref name="name"/> at <paramref name="startPoint"/>, and checks it out when <paramref name="checkout"/>.</summary>
    Task CreateAsync(
        RepositoryLocation location,
        string name,
        string startPoint,
        bool checkout,
        CancellationToken cancellationToken);

    /// <summary>Checks out an existing local branch.</summary>
    Task SwitchAsync(RepositoryLocation location, string name, CancellationToken cancellationToken);

    /// <summary>Creates a local branch tracking the remote branch (such as <c>origin/topic</c>) and checks it out.</summary>
    Task SwitchToRemoteAsync(RepositoryLocation location, string remoteBranch, CancellationToken cancellationToken);

    /// <summary>Checks out a commit with a detached HEAD.</summary>
    Task SwitchDetachedAsync(RepositoryLocation location, string commit, CancellationToken cancellationToken);

    /// <summary>Renames a local branch; its configuration (upstream) moves with it.</summary>
    Task RenameAsync(RepositoryLocation location, string oldName, string newName, CancellationToken cancellationToken);

    /// <summary>
    /// True when every commit of the branch is reachable from HEAD or from its upstream, which is when <c>git branch -d</c> agrees to
    /// delete it; false means deleting loses commits (or the answer could not be read).
    /// </summary>
    Task<bool> IsMergedAsync(
        RepositoryLocation location,
        string name,
        string? upstreamFullName,
        CancellationToken cancellationToken);

    /// <summary>Deletes a local branch; without <paramref name="force"/> git refuses when it is not merged.</summary>
    Task DeleteAsync(RepositoryLocation location, string name, bool force, CancellationToken cancellationToken);
}
