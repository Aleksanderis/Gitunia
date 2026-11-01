using Gitunia.Core.Effects;
using Gitunia.Core.Running;
using Gitunia.Core.Tags;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core;

/// <summary>
/// The root reducer <c>(AppState, AppCommand) -> (AppState, Effects)</c>: pure, no I/O, no clock. A case here holds no
/// logic; it delegates to the feature's reducer. Cases are in alphabetical order by feature.
/// </summary>
public static class AppReducer
{
    /// <summary>Applies a command and returns the new state and the effects to run.</summary>
    public static AppUpdate Reduce(AppState state, AppCommand command) => command switch
    {
        EffectFailedCommand failed => EffectFailed(state, failed),
        TagCommand tag => ReduceTag(state, tag),
        WorkspaceCommand workspace => ReduceWorkspace(state, workspace),
        _ => throw new UnhandledCommandException(command),
    };

    private static AppUpdate ReduceWorkspace(AppState state, WorkspaceCommand command)
    {
        var update = WorkspaceReducer.Reduce(state.Workspace, command);
        var effects = update.Effects.Cast<AppEffect>().ToList();
        if (command is CloseTabCommand close && state.Workspace.FindTab(close.TabId) is { } closed)
        {
            effects.AddRange(TagReducer.OnTabClosed(closed.Tags));
        }

        return new AppUpdate(new AppState(update.Workspace), [.. effects]);
    }

    // A result for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceTag(AppState state, TagCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Tags);
        var context = new TagContext(tab.Id, tab.Location, tab.Refs, workspace.ActiveTabId == tab.Id, busy);
        var update = TagReducer.Reduce(tab.Tags, command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        return new AppUpdate(WithTab(state, tab with { Tags = update.State, Running = running }), update.Effects);
    }

    private static AppUpdate EffectFailed(AppState state, EffectFailedCommand failed) =>
        new(state, [new PostStatusEffect(failed.Message, StatusSeverity.Error)]);

    private static AppState WithTab(AppState state, RepositoryState tab)
    {
        var tabs = state.Workspace.Tabs;
        for (var index = 0; index < tabs.Length; index++)
        {
            if (tabs[index].Id == tab.Id)
            {
                return new AppState(state.Workspace with { Tabs = tabs.SetItem(index, tab) });
            }
        }

        return state;
    }
}
