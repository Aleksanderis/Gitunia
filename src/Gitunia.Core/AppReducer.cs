using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Remotes;
using Gitunia.Core.Shell;
using Gitunia.Core.Stash;
using Gitunia.Core.Running;
using Gitunia.Core.Tags;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

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
        ReflogCommand reflog => ReduceReflog(state, reflog),
        SettingsCommand settings => ReduceSettings(state, settings),
        RemoteCommand remote => ReduceRemote(state, remote),
        ShellCommand shell => ReduceShell(state, shell),
        StashCommand stash => ReduceStash(state, stash),
        TabRenameCommand rename => ReduceTabRename(state, rename),
        TagCommand tag => ReduceTag(state, tag),
        WorkspaceCommand workspace => ReduceWorkspace(state, workspace),
        _ => throw new UnhandledCommandException(command),
    };

    // A command that follows (the accepted command of a confirm dialog) is reduced on the new state, after it.
    private static AppUpdate ReduceShell(AppState state, ShellCommand command)
    {
        var update = ShellReducer.Reduce(state.Shell, command);
        var shown = state with { Shell = update.State };
        if (update.Next is not { } next)
        {
            return new AppUpdate(shown, update.Effects);
        }

        var then = Reduce(shown, next);

        return new AppUpdate(then.State, [.. update.Effects, .. then.Effects]);
    }

    // A prompt belongs to the tab it was opened in and closes when that tab is closed or no longer the active one; the
    // reflog list closes with its tab.
    private static AppUpdate ReduceWorkspace(AppState state, WorkspaceCommand command)
    {
        var update = WorkspaceReducer.Reduce(state.Workspace, command);
        var shell = state.Shell;
        if (shell.Prompt is { Owner: { } owner } && update.Workspace.ActiveTabId != owner)
        {
            shell = ShellReducer.Reduce(shell, new PromptCancelledCommand()).State;
        }

        var changed = new AppState(update.Workspace, shell);
        if (shell.Reflog.List is { } list && update.Workspace.FindTab(list.TabId) is null)
        {
            var closed = ReduceReflog(changed, new ReflogTabClosed());

            return new AppUpdate(closed.State, [.. update.Effects, .. closed.Effects]);
        }

        return new AppUpdate(changed, [.. update.Effects]);
    }

    // A result for a tab that was closed meanwhile only ends the read.
    private static AppUpdate ReduceReflog(AppState state, ReflogCommand command)
    {
        var workspace = state.Workspace;
        var session = state.Shell.Reflog;
        var listTab = session.List is { } list ? workspace.FindTab(list.TabId) : null;
        if (command is ReflogLoaded loaded && workspace.FindTab(loaded.TabId) is null)
        {
            command = new ReflogReadCancelled();
        }

        var update = ReflogReducer.Reduce(session, command, new ReflogContext(workspace.ActiveTab, listTab));
        var shell = state.Shell with { Reflog = update.Session };

        return new AppUpdate(state with { Shell = shell }, update.Effects);
    }

    private static AppUpdate ReduceSettings(AppState state, SettingsCommand command)
    {
        var update = SettingsScreenReducer.Reduce(state.Shell.Settings, command);
        var shell = state.Shell with { Settings = update.Session };

        return new AppUpdate(state with { Shell = shell }, update.Effects);
    }

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceRemote(AppState state, RemoteCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Remotes);
        var context = new RemoteContext(tab.Id, tab.Location, tab.Refs, workspace.ActiveTabId == tab.Id, busy);
        var effects = RemoteReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        if (Equals(running, tab.Running))
        {
            return new AppUpdate(state, effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running }), effects);
    }

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceStash(AppState state, StashCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Stash);
        var context = new StashContext(tab.Id, tab.Location, tab.Stashes, tab.View.FocusedPane, busy);
        var effects = StashReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        if (Equals(running, tab.Running))
        {
            return new AppUpdate(state, effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running }), effects);
    }

    // A result for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceTabRename(AppState state, TabRenameCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var update = TabRenameReducer.Reduce(tab, command);

        return new AppUpdate(WithTab(state, update.Tab), update.Effects);
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
        var effects = TagReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        if (Equals(running, tab.Running))
        {
            return new AppUpdate(state, effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running }), effects);
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
                return state with { Workspace = state.Workspace with { Tabs = tabs.SetItem(index, tab) } };
            }
        }

        return state;
    }
}
