using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Fixup, reword and drop of one commit of the current branch from the graph, built on
/// <see cref="IInteractiveRebaseWriter"/> (the todo is prepared here, no editor). A merge commit is refused (merges
/// above it are kept, <c>--rebase-merges</c>), and so is a commit that is not in the history of HEAD.
/// </summary>
public interface IQuickRebaseWriter
{
    /// <summary>
    /// Reads what the edit would replay. Throws <see cref="RebaseRangeException"/> when the commit is a merge, is not
    /// in the history of HEAD, or (fixup) has no commit before it to meld into.
    /// </summary>
    Task<QuickRebasePlan> PlanAsync(
        RepositoryLocation location,
        QuickRebaseKind kind,
        string commit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs the plan. <paramref name="newSubject"/> replaces the first line of the message for a reword (the body is
    /// kept) and is ignored otherwise. A stop on conflicts is a result; the operation flow takes over.
    /// </summary>
    Task<InteractiveRebaseResult> RunAsync(
        RepositoryLocation location,
        QuickRebasePlan plan,
        string? newSubject,
        CancellationToken cancellationToken);
}
