using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.Git.Refs;

/// <summary>Reads HEAD, branches, tags and worktrees; the seam the loaders use so tests can replace git.</summary>
public interface IRefReader
{
    Task<ParseResult<GitRef>> ReadRefsAsync(RepositoryLocation location, CancellationToken cancellationToken);

    Task<ParseResult<WorktreeInfo>> ReadWorktreesAsync(RepositoryLocation location, CancellationToken cancellationToken);

    /// <summary>Reads what HEAD points to: a branch, a detached commit or an unborn branch.</summary>
    Task<HeadState> ReadHeadAsync(RepositoryLocation location, CancellationToken cancellationToken);
}
