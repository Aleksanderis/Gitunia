using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Creates commits; the seam the working-tree view uses so tests can replace git.</summary>
public interface ICommitWriter
{
    /// <summary>
    /// Commits the staged changes. Hooks run in the user's environment with stdin closed, so a hook or a signing program that
    /// asks for input fails; its output comes back in the <see cref="Execution.Exceptions.GitCommandFailedException"/>.
    /// </summary>
    Task<CommitResult> CommitAsync(RepositoryLocation location, CommitRequest request, CancellationToken cancellationToken);

    /// <summary>Reads the full message of HEAD (for an amend); null when there is no commit yet.</summary>
    Task<string?> ReadHeadMessageAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
