using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Reset;

/// <summary>
/// Moves the current branch of a worktree to a commit. Git refuses with the typed exception when it cannot (an
/// unknown commit); nothing is stashed or checked first, so the caller decides what is safe to ask. This is the seam
/// the reset action uses so tests can replace git.
/// </summary>
public interface IResetWriter
{
    /// <summary>
    /// Runs <c>git reset --soft|--mixed|--hard</c> to <paramref name="commit"/> and returns the hash HEAD had before,
    /// which <c>git reset &lt;hash&gt;</c> (or the reflog) returns to. The hash is read right before the reset.
    /// </summary>
    Task<string> ResetAsync(
        RepositoryLocation location,
        ResetMode mode,
        string commit,
        CancellationToken cancellationToken);
}
