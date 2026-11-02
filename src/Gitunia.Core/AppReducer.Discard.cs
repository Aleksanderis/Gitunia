using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Discard;
using Gitunia.Core.Running;
using Gitunia.Core.Visual;
using Gitunia.Platform.FileSystem;

namespace Gitunia.Core;

public static partial class AppReducer
{
    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand: a discard confirmed
    // after its tab was closed throws nothing away. A confirmed discard that started ends the Visual range it came from.
    private static AppUpdate ReduceDiscard(AppState state, RepositoryState tab, DiscardCommand command)
    {
        var context = new DiscardContext(
            tab.Id,
            tab.Location,
            tab.View.FocusedPane,
            tab.WorkingTree,
            tab.Running.IsRunning(RunningOperationKind.Discard) || tab.Running.IsRunning(RunningOperationKind.Staging),
            tab.Running.Excludes(RunningOperationKind.Discard),
            ByteExactFileSystems.IsSupportedHere);
        var effects = DiscardReducer.Reduce(command, context);
        var changed = ApplyRunning(state, tab, command, effects);
        if (command is DiscardConfirmed { EndsRange: true } && effects.Any(effect => effect is DiscardEffect))
        {
            var ended = VisualReducer.Reduce(changed, new VisualEnded());

            return new AppUpdate(ended.State, [.. effects, .. ended.Effects]);
        }

        return new AppUpdate(changed, effects);
    }
}
