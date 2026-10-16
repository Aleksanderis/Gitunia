using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Discard;

/// <summary>
/// Throws changes of the working tree away. Nothing here can be undone: git keeps no copy of what is lost, so callers confirm
/// with the user first. This is the seam the discard actions use so tests can replace git.
/// </summary>
public interface IWorkingTreeDiscarder
{
    /// <summary>
    /// Puts tracked files back as the index has them (<c>git restore --worktree</c>): edits are lost, a deleted file comes back and
    /// staged changes stay. Paths are relative to the repository root.
    /// </summary>
    Task RestoreAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes untracked files and directories (<c>git clean</c>); it never touches a tracked or an ignored path. A path that holds
    /// bytes which are not UTF-8 cannot be given to git as an argument and throws <see cref="NotSupportedException"/>.
    /// </summary>
    Task DeleteUntrackedAsync(RepositoryLocation location, IReadOnlyList<string> paths, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a patch backwards to the working tree only (<c>git apply -R</c>, never the index), which removes the changes it
    /// describes from the files. The patch goes on stdin as UTF-8; git refuses a patch that does not apply and changes nothing.
    /// </summary>
    Task ApplyReversePatchAsync(RepositoryLocation location, string patch, CancellationToken cancellationToken);
}
