using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.Git.Repositories.Models;

/// <summary>Everything a tab shows that comes from a refresh: HEAD, refs, worktrees, status and the running operation.</summary>
public sealed record RepositorySnapshot(
    HeadState Head,
    IReadOnlyList<GitRef> Refs,
    IReadOnlyList<WorktreeInfo> Worktrees,
    StatusSnapshot Status,
    RepositoryOperation Operation);
