using Gitunia.Git.Conflicts.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Conflicts;

/// <summary>Reads a conflicted path for the conflict editor; the seam the UI uses so tests can replace git.</summary>
public interface IConflictReader
{
    /// <param name="location">The repository (or linked worktree) the conflict is in.</param>
    /// <param name="path">
    /// The conflicted path, relative to the working tree root, as <c>git status</c> reports it. A path that is
    /// absolute or leaves the tree makes git fail (<c>GitCommandFailedException</c>).
    /// </param>
    /// <param name="cancellationToken">Cancels the git process and the file read.</param>
    Task<ConflictReadResult> ReadAsync(
        RepositoryLocation location,
        string path,
        CancellationToken cancellationToken = default);
}
