using Gitunia.Core.Commits;
using Gitunia.Core.Picker;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core;

public static partial class AppReducer
{
    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand: a stage-all question
    // answered after its tab was closed commits nothing. The commands a result asks for (the focus back to a list, the
    // commit that waited for stage all) are reduced after it, without settling in between: the state they read is
    // the one this command made.
    private static AppUpdate ReduceCommit(AppState state, CommitCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var context = new CommitContext(
            tab,
            state.Workspace.ActiveTabId == tab.Id,
            state.Shell.Mode,
            tab.Running.IsRunning(RunningOperationKind.Commit),
            BlocksCommit(tab));
        var update = CommitReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        var changed = state;
        if (!Equals(running, tab.Running) || !ReferenceEquals(update.State, tab.Commit))
        {
            changed = WithTab(state, tab with { Running = running, Commit = update.State });
        }

        var shell = changed.Shell;
        if (update.Mode is { } mode)
        {
            shell = shell.WithMode(mode);
        }

        if (!ReferenceEquals(update.State.Draft, tab.Commit.Draft))
        {
            // A changed draft is new unsent text: the next quit warns again.
            shell = shell with { QuitWarned = false };
        }

        if (update.Output is { } output)
        {
            shell = ShellReducer.Reduce(shell, output).State;
        }

        return FollowUnsettled(changed with { Shell = shell }, [.. update.Effects], update.Next);
    }

    // The draft of a tab that is replaced by a located one moves to the replacement; the picker learns whether it did.
    private static AppUpdate ReduceCommitDraftMove(AppState state, CommitDraftMoveRequested request)
    {
        var missing = state.Workspace.FindTab(request.Missing);
        var to = state.Workspace.FindTab(request.Target);
        var answer = new PickerDraftMoved(request.Missing, missing is null);
        var current = state;
        if (missing is not null && to is not null)
        {
            var moved = CommitDraftMove.Move(missing.Commit, to.Commit);
            answer = new PickerDraftMoved(request.Missing, moved.Moved);
            current = WithTab(WithTab(current, missing with { Commit = moved.From }), to with { Commit = moved.To });
        }

        return ReduceCommand(current, answer);
    }

    private static AppUpdate FollowUnsettled(AppState state, List<AppEffect> effects, IEnumerable<AppCommand> next)
    {
        var current = state;
        foreach (var command in next)
        {
            var then = ReduceCommand(current, command);
            current = then.State;
            effects.AddRange(then.Effects);
        }

        return new AppUpdate(current, [.. effects]);
    }

    // Committing is refused while a stage, discard or an operation that writes the index or the working tree runs.
    private static bool BlocksCommit(RepositoryState tab) =>
        tab.Running.IsRunning(RunningOperationKind.Staging)
        || tab.Running.IsRunning(RunningOperationKind.Discard)
        || BlocksIndexWriters(tab);
}
