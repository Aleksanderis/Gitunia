using Gitunia.Git.Execution;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Rebase;

/// <summary>
/// Plans and runs the quick rebases. A fixup melds the commit into its parent (base two commits back), a reword or a
/// drop starts at the parent; the root commit is reached with <see cref="RebaseBase.Root"/>. Every other commit of the
/// range is picked unchanged.
/// </summary>
public sealed class QuickRebaseWriter(GitRunner git, IInteractiveRebaseWriter interactive) : IQuickRebaseWriter
{
    private static readonly GitRunOptions ReadOptions = new(Timeout: TimeSpan.FromSeconds(30), ReadOnly: true);

    public async Task<QuickRebasePlan> PlanAsync(
        RepositoryLocation location,
        QuickRebaseKind kind,
        string commit,
        CancellationToken cancellationToken)
    {
        var parents = await ParentsAsync(location, commit, cancellationToken).ConfigureAwait(false);
        if (parents.Count > 1)
        {
            throw new RebaseRangeException("That is a merge commit; it cannot be edited by a rebase");
        }

        string? into = null;
        var start = parents.Count == 0 ? null : parents[0];
        if (kind == QuickRebaseKind.Fixup)
        {
            if (start is null)
            {
                throw new RebaseRangeException("The first commit has no commit before it to fix up into");
            }

            into = start;
            var grandParents = await ParentsAsync(location, start, cancellationToken).ConfigureAwait(false);
            if (grandParents.Count > 1)
            {
                throw new RebaseRangeException("The commit before it is a merge commit; it cannot take a fixup");
            }

            start = grandParents.Count == 0 ? null : grandParents[0];
        }

        var rebaseBase = start is null ? RebaseBase.Root : RebaseBase.OntoCommit(start);
        var range = await interactive.ReadRangeAsync(location, rebaseBase, cancellationToken).ConfigureAwait(false);
        var edited = range.ToList().FindIndex(item => item.Hash == commit);
        if (edited < 0)
        {
            throw new RebaseRangeException("That commit is not in the history of the current branch");
        }

        if (kind == QuickRebaseKind.Drop && rebaseBase.IsRoot && range.Count == 1)
        {
            throw new RebaseRangeException("It is the only commit of the branch: nothing would be left");
        }

        var target = into is null ? null : range.FirstOrDefault(item => item.Hash == into);
        var message = await MessageAsync(location, commit, cancellationToken).ConfigureAwait(false);
        var head = range[^1].Hash;
        return new QuickRebasePlan(kind, commit, message, rebaseBase, range, head, target);
    }

    public async Task<InteractiveRebaseResult> RunAsync(
        RepositoryLocation location,
        QuickRebasePlan plan,
        string? newSubject,
        CancellationToken cancellationToken)
    {
        var current = await interactive.ReadRangeAsync(location, plan.Base, cancellationToken).ConfigureAwait(false);
        if (!current.Select(item => item.Hash).SequenceEqual(plan.Range.Select(item => item.Hash)))
        {
            throw new RebaseRangeException("The history changed since it was read; nothing was rebased");
        }

        var entries = current.Select(item => EntryFor(item, plan, newSubject)).ToList();
        return await interactive.RebaseAsync(location, plan.Base, entries, cancellationToken).ConfigureAwait(false);
    }

    private static RebaseTodoEntry EntryFor(RebaseTodoCommit item, QuickRebasePlan plan, string? newSubject)
    {
        if (item.Hash != plan.Commit)
        {
            return new RebaseTodoEntry(item.Hash, RebaseTodoAction.Pick);
        }

        return plan.Kind switch
        {
            QuickRebaseKind.Fixup => new RebaseTodoEntry(item.Hash, RebaseTodoAction.Fixup),
            QuickRebaseKind.Drop => new RebaseTodoEntry(item.Hash, RebaseTodoAction.Drop),
            _ => new RebaseTodoEntry(item.Hash, RebaseTodoAction.Reword, Reworded(plan.Message, newSubject)),
        };
    }

    // The new first line over the old body, so a reword of the subject never loses the rest of the message.
    private static string Reworded(string message, string? newSubject)
    {
        if (string.IsNullOrWhiteSpace(newSubject))
        {
            throw new RebaseRangeException("The new subject is empty");
        }

        var end = message.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? newSubject.Trim() : $"{newSubject.Trim()}{message[end..]}";
    }

    private async Task<List<string>> ParentsAsync(
        RepositoryLocation location,
        string commit,
        CancellationToken cancellationToken)
    {
        var result = await git
            .RunAsync(
                location.CommandPath,
                ["rev-list", "--parents", "-n", "1", commit, GitOptions.EndOfOptions],
                ReadOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return [.. result.StandardOutputText.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Skip(1)];
    }

    private async Task<string> MessageAsync(
        RepositoryLocation location,
        string commit,
        CancellationToken cancellationToken)
    {
        var result = await git
            .RunAsync(
                location.CommandPath,
                ["log", "-n", "1", "--format=%B", commit, GitOptions.EndOfOptions],
                ReadOptions,
                cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutputText.TrimEnd();
    }
}
