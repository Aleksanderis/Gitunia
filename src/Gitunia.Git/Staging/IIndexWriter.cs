using Gitunia.Git.Diff.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Staging;

/// <summary>Changes the index of a repository or worktree; the seam the staging actions use so tests can replace git.</summary>
public interface IIndexWriter
{
    /// <summary>Stages whole files (<c>git add</c>); a deleted path stages its deletion. Paths are relative to the repository root.</summary>
    Task StageAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// Records untracked files as intent-to-add (<c>git add -N</c>): the index gets an empty entry, so the whole content shows as an
    /// unstaged addition that a partial patch can then stage. The caller must remove the entry again (<see cref="UnstageAsync"/>) when
    /// it does not go on to stage something.
    /// </summary>
    Task IntentToAddAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// Unstages whole files: resets their index entries to HEAD (<c>git restore --staged</c>), or removes them from the index
    /// (<c>git rm --cached</c>) when the branch is unborn and there is no HEAD to restore from.
    /// </summary>
    Task UnstageAsync(RepositoryLocation location, IReadOnlyList<string> paths, bool isHeadUnborn, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a patch to the index only (<c>git apply --cached</c>, with <c>-R</c> for <see cref="PatchDirection.Reverse"/>), the way
    /// hunks and lines are staged and unstaged. The patch goes on stdin as UTF-8; git refuses a patch that does not apply and leaves
    /// the index as it was.
    /// </summary>
    Task ApplyPatchAsync(RepositoryLocation location, string patch, PatchDirection direction, CancellationToken cancellationToken);
}
