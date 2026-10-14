using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Detects a stopped multi-step operation (rebase, merge, ...) of a worktree; the seam the refresher uses so tests can fake the operation section.</summary>
public interface IRepositoryOperationDetector
{
    /// <summary>Detects a stopped multi-step operation of the worktree <paramref name="location"/> names.</summary>
    RepositoryOperation Detect(RepositoryLocation location);
}
