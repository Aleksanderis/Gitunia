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
    /// the range contains a merge commit (rebasing it would flatten it) and <see cref="RebaseBase.KeepMerges"/> is not
    /// set, or the range is empty. With it set and a merge in the range, git prepares the <c>--rebase-merges</c> todo
    /// and the result is that todo in order: <see cref="RebaseTodoAction.Label"/>, <see cref="RebaseTodoAction.Reset"/>
    /// and <see cref="RebaseTodoAction.Merge"/> lines between the commits.
    /// </summary>
    Task<IReadOnlyList<RebaseTodoCommit>> ReadRangeAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs <c>git rebase -i</c> with <paramref name="entries"/> as the todo. The entries must name every commit of the
    /// range exactly once and keep its label, reset and merge lines (use <see cref="RebaseTodoAction.Drop"/> to remove
    /// one); the first one that is not dropped cannot be a squash or fixup; a reword needs a message.
    /// Anything else, and a merge commit in the range without <see cref="RebaseBase.KeepMerges"/>, throws
    /// <see cref="RebaseRangeException"/> before git is started. Git's own refusals (dirty tree, hooks) throw the typed
    /// exception.
    /// </summary>
    Task<InteractiveRebaseResult> RebaseAsync(
        RepositoryLocation location,
        RebaseBase rebaseBase,
        IReadOnlyList<RebaseTodoEntry> entries,
        CancellationToken cancellationToken);
}
