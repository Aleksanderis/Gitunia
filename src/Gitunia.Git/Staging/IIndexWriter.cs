using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Staging;

/// <summary>Changes the index of a repository or worktree; the seam the staging actions use so tests can replace git.</summary>
public interface IIndexWriter
{
    /// <summary>Stages whole files (<c>git add</c>); a deleted path stages its deletion. Paths are relative to the repository root.</summary>
    Task StageAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// Unstages whole files: resets their index entries to HEAD (<c>git restore --staged</c>), or removes them from the index
    /// (<c>git rm --cached</c>) when the branch is unborn and there is no HEAD to restore from.
    /// </summary>
    Task UnstageAsync(RepositoryLocation location, IReadOnlyList<string> paths, bool isHeadUnborn, CancellationToken cancellationToken);
}
