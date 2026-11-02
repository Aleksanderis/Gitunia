using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Integration;
using Gitunia.Core.Operations;
using Gitunia.Core.Remotes;
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

public static partial class AppReducer
{
    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceIntegration(AppState state, IntegrationCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var busy = IsBusy(tab, RunningOperationKind.Integration);
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
        var syncing = tab.Running.Excludes(RunningOperationKind.Remotes);
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

        var busy = IsBusy(tab, RunningOperationKind.Stash);
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
        var other = tab.Running.Excludes(RunningOperationKind.Sync);
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

        var busy = IsBusy(tab, RunningOperationKind.Branches);
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

        var busy = IsBusy(tab, RunningOperationKind.Tags);
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

    // The kind runs on the tab, or another running operation excludes it (the table is OperationExclusion).
    private static bool IsBusy(RepositoryState tab, RunningOperationKind kind) =>
        tab.Running.IsRunning(kind) || tab.Running.Excludes(kind);

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
