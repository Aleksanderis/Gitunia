using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Refs;

/// <summary>Answers whether one commit is contained in the history of another.</summary>
public interface IAncestryReader
{
    /// <summary>
    /// True when <paramref name="ancestor"/> is reachable from <paramref name="descendant"/> (a commit is its own
    /// ancestor); false when it is not. Both are commit ids; an unknown id throws the typed git exception.
    /// </summary>
    Task<bool> IsAncestorAsync(
        RepositoryLocation location,
        string ancestor,
        string descendant,
        CancellationToken cancellationToken);
}
