using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Reads the details of one commit; the seam the details store uses so tests can replace git.</summary>
public interface ICommitDetailsReader
{
    /// <param name="location">The repository.</param>
    /// <param name="id">The commit to read.</param>
    /// <param name="warnings">Receives the parsers' warnings.</param>
    /// <param name="cancellationToken">Cancels both git processes.</param>
    /// <returns>The details; null when git printed no commit.</returns>
    Task<CommitDetails?> ReadAsync(RepositoryLocation location, ObjectId id, ICollection<string>? warnings = null, CancellationToken cancellationToken = default);
}
