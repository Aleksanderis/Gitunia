using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Config;

/// <summary>Reads settings of the user's git config; the seam the refresh uses so tests can replace git.</summary>
public interface IGitConfigReader
{
    /// <summary>Whether <c>rebase.autoStash</c> is on for the repository (any scope), false when it is not set.</summary>
    Task<bool> ReadRebaseAutoStashAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
