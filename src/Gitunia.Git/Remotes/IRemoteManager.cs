using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Remotes;

/// <summary>
/// Adds, removes, renames and re-points remotes (<c>git remote</c>), and lists them. Config-only: nothing is fetched.
/// Every write throws the typed git exception when git refuses. The seam the remote management actions use.
/// </summary>
public interface IRemoteManager
{
    /// <summary>The configured remotes with their fetch URLs, in git's order.</summary>
    Task<IReadOnlyList<RemoteInfo>> ListAsync(RepositoryLocation location, CancellationToken cancellationToken);

    /// <summary>Adds the remote <paramref name="name"/> with <paramref name="url"/>; nothing is fetched.</summary>
    Task AddAsync(RepositoryLocation location, string name, string url, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the remote, its remote-tracking branches and the upstream settings that pointed at it (git's own
    /// behaviour).
    /// </summary>
    Task RemoveAsync(RepositoryLocation location, string name, CancellationToken cancellationToken);

    /// <summary>Renames the remote; git moves its remote-tracking branches and upstream settings along.</summary>
    Task RenameAsync(RepositoryLocation location, string name, string newName, CancellationToken cancellationToken);

    /// <summary>Changes the URL of the remote.</summary>
    Task SetUrlAsync(RepositoryLocation location, string name, string url, CancellationToken cancellationToken);
}
