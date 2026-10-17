using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Merge;

/// <summary>Merges a branch or commit into the checked-out branch of a worktree, without an editor.</summary>
public interface IMergeWriter
{
    /// <summary>
    /// Runs <c>git merge</c> of <paramref name="target"/> (a branch name or a commit hash) into HEAD. Git's default
    /// message is used. A stop on conflicts is a <see cref="MergeKind.Conflicted"/> result; every other refusal (a
    /// dirty tree git cannot merge over, an unknown target) throws.
    /// </summary>
    Task<MergeResult> MergeAsync(
        RepositoryLocation location,
        string target,
        MergeMode mode,
        CancellationToken cancellationToken);
}
