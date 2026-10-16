using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Git.Stash;

/// <summary>
/// Changes the stash stack and applies stashes; the seam the stash actions use so tests can replace git. An entry is
/// named by its commit hash: the writer finds its current <c>stash@{n}</c> first, which narrows the window in which a
/// stack changed by another process could redirect the call to a different stash.
/// </summary>
public interface IStashWriter
{
    /// <summary>
    /// Stashes the tracked changes (<c>git stash push</c>), plus the untracked files when asked; ignored files never.
    /// An empty <paramref name="message"/> keeps git's default subject.
    /// </summary>
    Task<StashResult> PushAsync(
        RepositoryLocation location,
        string message,
        bool includeUntracked,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a stash and keeps it. A conflict that this call made is <see cref="StashOutcome.Conflicted"/>, not an
    /// exception; git refusing (local changes would be overwritten, a merge already stopped on conflicts) is a
    /// <see cref="Execution.Exceptions.GitCommandFailedException"/> and changes nothing.
    /// </summary>
    Task<StashResult> ApplyAsync(RepositoryLocation location, StashEntry entry, CancellationToken cancellationToken);

    /// <summary>Applies a stash and drops it when it applied cleanly; a conflict keeps the stash.</summary>
    Task<StashResult> PopAsync(RepositoryLocation location, StashEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Drops a stash. The commit stays in the object database until git collects garbage, so
    /// <c>git stash store -m "&lt;subject&gt;" &lt;hash&gt;</c> brings it back.
    /// </summary>
    Task<StashResult> DropAsync(RepositoryLocation location, StashEntry entry, CancellationToken cancellationToken);
}
