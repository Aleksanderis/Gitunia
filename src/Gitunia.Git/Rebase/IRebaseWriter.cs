using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Starts a non-interactive rebase of the current branch of a worktree. A stop on conflicts is a result, not an
/// exception; the banner and continue/abort/skip of the operation flow take over from there. This is the seam the
/// rebase action uses so tests can replace git.
/// </summary>
public interface IRebaseWriter
{
    /// <summary>
    /// Runs <c>git rebase &lt;onto&gt;</c> with no editor. <paramref name="onto"/> is a commit hash or ref name that
    /// does not start with <c>-</c>. Returns <see cref="OperationOutcome.Conflicted"/> when the rebase stopped on
    /// conflicts; any other failure (a dirty tree, an unknown commit, a refusing hook) throws the typed exception.
    /// </summary>
    Task<OperationResult> RebaseAsync(RepositoryLocation location, string onto, CancellationToken cancellationToken);

    /// <summary>
    /// How many merge commits a plain rebase onto <paramref name="onto"/> would flatten: the merge commits among the
    /// commits of HEAD that <paramref name="onto"/> does not have (<c>git rev-list --merges</c>).
    /// </summary>
    Task<int> CountMergesAsync(RepositoryLocation location, string onto, CancellationToken cancellationToken);
}
