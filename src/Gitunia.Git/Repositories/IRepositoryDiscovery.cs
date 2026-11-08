using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Resolves a path to its repository; the seam the effect handlers use so tests can hold the discovery.</summary>
public interface IRepositoryDiscovery
{
    /// <summary>Resolves <paramref name="path"/> (root, subdirectory, linked worktree, bare repository) to its location.</summary>
    Task<RepositoryLocation> DiscoverAsync(string path, CancellationToken cancellationToken);
}
