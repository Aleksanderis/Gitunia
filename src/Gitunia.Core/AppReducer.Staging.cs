using Gitunia.Core.Running;
using Gitunia.Core.Staging;

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
            tab.Running.Excludes(RunningOperationKind.Staging));
        var update = StagingReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        var changed = Equals(running, tab.Running) ? state : WithTab(state, tab with { Running = running });

        return Follow(changed, [.. update.Effects], update.Next);
    }
}
