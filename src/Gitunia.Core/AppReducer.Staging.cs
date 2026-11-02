using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Running;
using Gitunia.Core.Staging;

namespace Gitunia.Core;

public static partial class AppReducer
{
    // The commands a result
    // asks for (the commit that waited for stage all, the cursor after a resolved file) are reduced after it.
    private static AppUpdate ReduceStaging(AppState state, RepositoryState tab, StagingCommand command)
    {
        var context = new StagingContext(
            tab.Id,
            tab.Location,
            tab.View.FocusedPane,
            tab.WorkingTree,
            tab.Operation is not null,
            tab.Running.IsRunning(RunningOperationKind.Staging),
            tab.Running.Excludes(RunningOperationKind.Staging));
        var update = StagingReducer.Reduce(command, context);
        var changed = ApplyRunning(state, tab, command, update.Effects);

        return Follow(changed, [.. update.Effects], update.Next);
    }
}
