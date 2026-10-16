using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>
/// Continues, aborts or skips the stopped operation of a worktree. Every call runs without an editor (git's prepared
/// message is used) and returns an <see cref="OperationResult"/>: stopping on the next conflict is a result.
/// </summary>
public interface IOperationWriter
{
    /// <summary>Runs <c>&lt;operation&gt; --continue</c>; a rebase may stop again at the next commit.</summary>
    Task<OperationResult> ContinueAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken);

    /// <summary>Runs <c>&lt;operation&gt; --abort</c>: the work of the operation is undone and the old tip
    /// restored.</summary>
    Task<OperationResult> AbortAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken);

    /// <summary>Runs <c>&lt;operation&gt; --skip</c> (rebase, cherry-pick and revert; a merge cannot be
    /// skipped).</summary>
    Task<OperationResult> SkipAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken);
}
