using Gitunia.Core.Ignore;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core;

public static partial class AppReducer
{
    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceIgnore(AppState state, RepositoryState tab, IgnoreCommand command)
    {
        var context = new IgnoreContext(
            tab.Id,
            tab.Location,
            tab.View.FocusedPane,
            tab.WorkingTree,
            tab.Running.IsRunning(RunningOperationKind.Ignore));
        var effects = IgnoreReducer.Reduce(command, context);

        return new AppUpdate(ApplyRunning(state, tab, command, effects), effects);
    }
}
