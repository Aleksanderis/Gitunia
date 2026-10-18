using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Commits;

/// <summary>Finds commits by their whole message (subject and body); the graph rows keep only the subject.</summary>
public interface ICommitMessageSearcher
{
    /// <param name="location">The repository to search.</param>
    /// <param name="text">Plain text (not a pattern); compared without regard to case.</param>
    /// <param name="cancellationToken">Cancels the search and kills git.</param>
    /// <returns>
    /// The commits reachable from any ref except the stash whose message contains <paramref name="text"/>.
    /// </returns>
    Task<IReadOnlySet<ObjectId>> SearchAsync(
        RepositoryLocation location,
        string text,
        CancellationToken cancellationToken);
}
