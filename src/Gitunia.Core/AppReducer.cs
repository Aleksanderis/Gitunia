using Gitunia.Core.Branches;
using Gitunia.Core.Creation;
using Gitunia.Core.Picker;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Pick;
using Gitunia.Core.Integration;
using Gitunia.Core.Operations;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Remotes;
using Gitunia.Core.Shell;
using Gitunia.Core.Stash;
using Gitunia.Core.Running;
using Gitunia.Core.Sync;
using Gitunia.Core.Tags;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git;

namespace Gitunia.Core;

/// <summary>
/// The root reducer <c>(AppState, AppCommand) -> (AppState, Effects)</c>: pure, no I/O, no clock. A case here holds no
/// logic; it delegates to the feature's reducer. Cases are in alphabetical order by feature.
/// </summary>
public static partial class AppReducer
{
    /// <summary>Applies a command and returns the new state and the effects to run.</summary>
    public static AppUpdate Reduce(AppState state, AppCommand command) => command switch
    {
        BranchCommand branch => ReduceBranch(state, branch),
        CreationCommand creation => ReduceCreation(state, creation),
        EffectFailedCommand failed => EffectFailed(state, failed),
        PickCommand pick => ReducePick(state, pick),
        RebaseCommand rebase => ReduceRebase(state, rebase),
        IntegrationCommand integration => ReduceIntegration(state, integration),
        PickerCommand picker => ReducePicker(state, picker),
        QuitCommand quit => ReduceQuit(state, quit),
        ReflogCommand reflog => ReduceReflog(state, reflog),
        SettingsCommand settings => ReduceSettings(state, settings),
        RemoteCommand remote => ReduceRemote(state, remote),
        ShellCommand shell => ReduceShell(state, shell),
        StashCommand stash => ReduceStash(state, stash),
        SyncCommand sync => ReduceSync(state, sync),
        TabRenameCommand rename => ReduceTabRename(state, rename),
        TagCommand tag => ReduceTag(state, tag),
        WorkspaceActionPressed action => ReduceWorkspaceAction(state, action),
        WorkspaceCommand workspace => ReduceWorkspace(state, workspace),
        WorktreeCommand worktree => ReduceWorktree(state, worktree),
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

        if (shell.Mainline is { } question && update.Workspace.ActiveTabId != question.TabId)
        {
            shell = (shell with { Mainline = null }).WithMode(KeyMode.Normal);
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

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand. The mainline question
    // lives in the shell state because it is an overlay; the reducer returns it after each command.
    private static AppUpdate ReducePick(AppState state, PickCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Pick);
        var question = state.Shell.Mainline is { } open && open.TabId == tab.Id ? open : null;
        var context = new PickContext(
            tab.Id,
            tab.Location,
            tab.Head,
            tab.Operation,
            workspace.ActiveTabId == tab.Id,
            busy,
            question);
        var update = PickReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        var next = state;
        if (!Equals(running, tab.Running))
        {
            next = WithTab(state, tab with { Running = running });
        }

        if (!Equals(update.Question, question))
        {
            next = next with { Shell = next.Shell with { Mainline = update.Question } };
        }

        return new AppUpdate(next, update.Effects);
    }

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceRebase(AppState state, RebaseCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Rebase);
        var context = new RebaseContext(tab, workspace.ActiveTabId == tab.Id, busy, IsOverlayOpen(state.Shell));
        var effects = RebaseReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        if (Equals(running, tab.Running))
        {
            return new AppUpdate(state, effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running }), effects);
    }

    // A late question must not replace what the user is looking at.
    private static bool IsOverlayOpen(ShellState shell) =>
        shell.Confirm is not null
        || shell.Output is not null
        || shell.Prompt is not null
        || shell.Mainline is not null
        || shell.Reflog.IsOpen
        || shell.Settings.IsOpen;

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceIntegration(AppState state, IntegrationCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Integration);
        var conflicts = OperationBannerBuilder.ConflictCount(tab.WorkingTree);
        var context = new IntegrationContext(tab.Id, tab.Location, tab.Head, tab.Operation, conflicts, busy);
        var effects = IntegrationReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        if (Equals(running, tab.Running))
        {
            return new AppUpdate(state, effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running }), effects);
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
        var syncing = tab.Running.IsRunning(RunningOperationKind.Sync);
        var context = new RemoteContext(tab.Id, tab.Location, tab.Refs, workspace.ActiveTabId == tab.Id, busy, syncing);
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

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceSync(AppState state, SyncCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var shell = state.Shell;
        var overlay = shell.Confirm is not null || shell.Prompt is not null || shell.Output is not null;
        var busy = tab.Running.IsRunning(RunningOperationKind.Sync);
        var other = tab.Running.Items.Any(item => item.Kind != RunningOperationKind.Sync);
        var context = new SyncContext(
            tab.Id,
            tab.Location,
            tab.Refs,
            workspace.ActiveTabId == tab.Id,
            busy,
            other,
            overlay,
            tab.Sync);
        var update = SyncReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        if (Equals(running, tab.Running) && Equals(update.State, tab.Sync))
        {
            return new AppUpdate(state, update.Effects);
        }

        return new AppUpdate(WithTab(state, tab with { Running = running, Sync = update.State }), update.Effects);
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

    // A result for a tab that was closed meanwhile is dropped, like a late TabCommand. After a success the graph cursor
    // follows to the commit the reducer named; the saved selection is the commit the refreshed graph then selects.
    private static AppUpdate ReduceBranch(AppState state, BranchCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Branches);
        var context = new BranchContext(tab.Id, tab.Location, tab.Refs, tab.Operation, busy);
        var effects = BranchReducer.Reduce(command, context);
        var running = tab.Running.Apply(tab.Id, command, effects);
        var next = state;
        if (!Equals(running, tab.Running))
        {
            next = WithTab(state, tab with { Running = running });
        }

        if (command is BranchOperationSucceeded { Follow: { } follow } && ObjectId.TryParse(follow, out var commit))
        {
            var selected = WorkspaceReducer.Reduce(
                next.Workspace,
                new TabCommand(tab.Id, new SelectCommitCommand(commit)));
            next = next with { Workspace = selected.Workspace };
            effects = [.. effects, .. selected.Effects];
        }

        return new AppUpdate(next, effects);
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

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand. A located worktree or a
    // read overview is applied to the workspace after the feature has decided.
    private static AppUpdate ReduceWorktree(AppState state, WorktreeCommand command)
    {
        var workspace = state.Workspace;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = tab.Running.IsRunning(RunningOperationKind.Worktrees);
        var update = WorktreeReducer.Reduce(command, new WorktreeContext(tab, workspace.Tabs, busy));
        var running = tab.Running.Apply(tab.Id, command, update.Effects);
        var next = Equals(running, tab.Running) ? state : WithTab(state, tab with { Running = running });
        if (update.Workspace is not { } then)
        {
            return new AppUpdate(next, update.Effects);
        }

        var applied = ReduceWorkspace(next, then);

        return new AppUpdate(applied.State, [.. update.Effects, .. applied.Effects]);
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
