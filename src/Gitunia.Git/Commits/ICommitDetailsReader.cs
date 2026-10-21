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

    /// <summary>Reads the files <paramref name="id"/> changed against one parent (a merge's per-parent view).</summary>
    /// <param name="location">The repository.</param>
    /// <param name="id">The commit.</param>
    /// <param name="parent">The parent to compare against, null for a root commit (against the empty tree).</param>
    /// <param name="warnings">Receives the parser's warnings.</param>
    /// <param name="cancellationToken">Cancels git.</param>
    Task<IReadOnlyList<CommitFileChange>> ReadFilesAsync(
        RepositoryLocation location,
        ObjectId id,
        ObjectId? parent,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default);
}
