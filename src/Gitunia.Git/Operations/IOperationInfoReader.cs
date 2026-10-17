using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Operations;

/// <summary>Reads the details of the stopped operation of a worktree; the seam tests fake.</summary>
public interface IOperationInfoReader
{
    /// <summary>Reads the details of <paramref name="operation"/>, null for <see cref="RepositoryOperation.None"/> or
    /// one the banner does not handle.</summary>
    Task<OperationInfo?> ReadAsync(
        RepositoryLocation location,
        RepositoryOperation operation,
        CancellationToken cancellationToken);
}
