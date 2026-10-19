using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Interactive rebase without an editor: the caller reads the commits of the range, arranges them as todo entries and
/// starts the rebase with them. A stop on conflicts is a result and the operation flow (banner, continue, skip, abort)
/// takes over; the editor UI is built on this seam.
/// </summary>
public interface IInteractiveRebaseWriter
{
    /// <summary>
    /// Reads the commits of the range in replay order (oldest first). Throws <see cref="RebaseRangeException"/> when
    /// the range contains a merge commit (rebasing it would flatten it; <c>--rebase-merges</c> is not supported) or is
    /// empty.
    /// </summary>
    Task<IReadOnlyList<RebaseTodoCommit>> ReadRangeAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs <c>git rebase -i</c> with <paramref name="entries"/> as the todo. The entries must name every commit of the
    /// range exactly once (use <see cref="RebaseTodoAction.Drop"/> to remove one); the first one that is not dropped
    /// cannot be a squash or fixup; a reword needs a message. Anything else, and a merge commit in the range, throws
    /// <see cref="RebaseRangeException"/> before git is started. Git's own refusals (dirty tree, hooks) throw the typed
    /// exception.
    /// </summary>
    Task<InteractiveRebaseResult> RebaseAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        IReadOnlyList<RebaseTodoEntry> entries,
        CancellationToken cancellationToken);
}
