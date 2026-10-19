using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Trees.Models;

namespace Gitunia.Git.Trees;

/// <summary>Reads the files of a commit's tree; the seam the tree store uses so tests can replace git.</summary>
public interface ICommitTreeReader
{
    /// <param name="location">The repository.</param>
    /// <param name="commit">The commit whose tree to list.</param>
    /// <param name="warnings">Receives the parser's warnings.</param>
    /// <param name="cancellationToken">Cancels the git process.</param>
    /// <returns>Every file of the tree in git's order.</returns>
    Task<IReadOnlyList<TreeEntry>> ReadAsync(
        RepositoryLocation location,
        ObjectId commit,
        ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default);
}
