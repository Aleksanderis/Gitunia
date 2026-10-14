namespace Gitunia.Git.Worktrees;

/// <summary>One entry of <c>git worktree list</c>.</summary>
/// <param name="Path">Absolute path of the worktree (the git directory for a bare main entry).</param>
/// <param name="HeadHash">Commit HEAD points at; null for an unborn branch (git reports all zeros) and for a bare entry.</param>
/// <param name="BranchFullName">Full name of the checked-out branch, or null when detached or bare.</param>
/// <param name="IsDetached">HEAD is detached.</param>
/// <param name="IsBare">The entry is the bare repository itself.</param>
/// <param name="IsLocked">The worktree is locked against pruning and removal.</param>
/// <param name="LockReason">Why it is locked, when git recorded a reason.</param>
/// <param name="IsPrunable">The worktree's directory is gone and it can be pruned.</param>
/// <param name="PruneReason">Why it can be pruned.</param>
/// <param name="IsMain">The first entry: the main working tree (or the bare repository).</param>
public sealed record WorktreeInfo(
    string Path,
    string? HeadHash,
    string? BranchFullName,
    bool IsDetached,
    bool IsBare,
    bool IsLocked,
    string? LockReason,
    bool IsPrunable,
    string? PruneReason,
    bool IsMain);
