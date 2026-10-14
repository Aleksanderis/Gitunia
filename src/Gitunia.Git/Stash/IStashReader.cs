using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Stash;

/// <summary>Reads the stash stack; the seam the loaders use so tests can replace git.</summary>
public interface IStashReader
{
    Task<ParseResult<StashEntry>> ReadStashesAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
