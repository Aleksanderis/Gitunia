using Gitunia.Core.Running;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core;

public static partial class AppReducer
{
    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand. The commands a result
    // asks for (the commit that waited for stage all, the cursor after a resolved file) are reduced after it.
    private static AppUpdate ReduceStaging(AppState state, StagingCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var context = new StagingContext(
            tab.Id,
            tab.Location,
            tab.View.FocusedPane,
            tab.WorkingTree,
            tab.Operation is not null,
            tab.Running.IsRunning(RunningOperationKind.Staging),
            BlocksStaging(tab));
        var update = StagingReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        var changed = Equals(running, tab.Running) ? state : WithTab(state, tab with { Running = running });

        return Follow(changed, [.. update.Effects], update.Next);
    }

    // Staging is refused while a discard or an operation that writes the index or the working tree of the tab runs.
    private static bool BlocksStaging(RepositoryState tab) =>
        tab.Running.IsRunning(RunningOperationKind.Discard) || BlocksIndexWriters(tab);

    // The operations that write the index or the working tree and exclude staging and discarding in both directions.
    private static bool BlocksIndexWriters(RepositoryState tab) =>
        tab.Running.IsRunning(RunningOperationKind.Branches)
        || tab.Running.IsRunning(RunningOperationKind.Integration)
        || tab.Running.IsRunning(RunningOperationKind.Pick)
        || tab.Running.IsRunning(RunningOperationKind.Rebase)
        || tab.Running.IsRunning(RunningOperationKind.Stash);

    // A stage, unstage or discard of the tab runs: the other operations that write the tree refuse to start.
    private static bool IsStaging(RepositoryState tab) =>
        tab.Running.IsRunning(RunningOperationKind.Staging) || tab.Running.IsRunning(RunningOperationKind.Discard);
}
