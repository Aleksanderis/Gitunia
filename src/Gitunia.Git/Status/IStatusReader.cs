using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Status;

/// <summary>Reads the working tree status; the seam the loaders use so tests can replace git.</summary>
public interface IStatusReader
{
    /// <summary>Reads the status; untracked files are listed individually unless <paramref name="untrackedFiles"/> says otherwise.</summary>
    Task<StatusSnapshot> ReadStatusAsync(RepositoryLocation location, bool includeIgnored, UntrackedFilesMode untrackedFiles, CancellationToken cancellationToken);
}
