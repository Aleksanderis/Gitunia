using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Reads the commit log of a repository; the seam the loaders use so tests can replace git.</summary>
public interface ICommitLogReader
{
    /// <param name="location">The repository to read.</param>
    /// <param name="order">Commit order of the log.</param>
    /// <param name="revisions">Revision arguments such as <c>--all</c>, <c>HEAD</c> or a branch name; every ref except the stash if empty (stash commits are not history).</param>
    /// <param name="warnings">Receives the parser's warnings (skipped or truncated records) once the stream ends.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    IAsyncEnumerable<CommitInfo> ReadAsync(
        RepositoryLocation location,
        CommitOrder order,
        IReadOnlyList<string> revisions,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default);

    /// <summary>Number of commits <see cref="ReadAsync"/> returns for the default revisions (every ref except the stash), or -1 when no number came back.</summary>
    Task<int> CountAsync(RepositoryLocation location, CancellationToken cancellationToken);

    /// <summary>The decorations of the commits that carry a ref (a few hundred at most), exactly as <see cref="ReadAsync"/> reports them; reads no history.</summary>
    Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
