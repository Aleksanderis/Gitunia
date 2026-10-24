using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>Brings a file back to its conflicted state, the way back from a bad resolution.</summary>
public interface IConflictRecreator
{
    /// <summary>
    /// Re-creates the merge of a file (<c>checkout --merge</c>): the working tree file gets the conflict markers again
    /// (in the user's <c>merge.conflictStyle</c>) and, for a file already marked resolved, the index its conflicted
    /// stages. The file's current content is overwritten.
    /// </summary>
    /// <param name="location">The repository (or linked worktree).</param>
    /// <param name="path">The file, relative to the working tree root.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <exception cref="Gitunia.Git.Execution.Exceptions.GitCommandFailedException">
    /// Git refused, for example because the file has no recorded conflict to re-create.
    /// </exception>
    Task RecreateAsync(RepositoryLocation location, string path, CancellationToken cancellationToken = default);
}
