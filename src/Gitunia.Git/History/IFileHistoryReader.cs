using Gitunia.Git.History.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.History;

/// <summary>
/// Reads the history of one file across renames. The seam the file history view uses so tests can replace it.
/// </summary>
public interface IFileHistoryReader
{
    /// <summary>
    /// Reads at most <paramref name="limit"/> commits that touched <paramref name="path"/>, newest first, following
    /// renames, starting from <paramref name="revision"/>. An empty list means the file has no history there.
    /// </summary>
    /// <param name="location">The repository.</param>
    /// <param name="revision">Where the walk starts (a commit hash or <c>HEAD</c>).</param>
    /// <param name="path">The repository-relative path of the file.</param>
    /// <param name="limit">The most commits to read.</param>
    /// <param name="cancellationToken">Cancels the read and kills git.</param>
    Task<ParseResult<FileHistoryEntry>> ReadAsync(
        RepositoryLocation location,
        string revision,
        string path,
        int limit,
        CancellationToken cancellationToken);
}
