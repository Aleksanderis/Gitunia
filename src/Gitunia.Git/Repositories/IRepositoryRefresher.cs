using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Recomputes a tab's snapshot after a change; the seam the tab refresher uses so tests can replace git.</summary>
public interface IRepositoryRefresher
{
    /// <summary>Reads the repository again for <paramref name="kinds"/>; with no <paramref name="previous"/> snapshot everything is read.</summary>
    Task<RepositoryRefreshResult> RefreshAsync(RepositoryLocation location, RepositorySnapshot? previous, RepositoryChangeKinds kinds, CancellationToken cancellationToken);
}
