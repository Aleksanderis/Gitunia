using System.Collections.Immutable;
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
    // The feature reducers below share one shape (see ReduceInTab): the tab's context is built in a static lambda.
    private static AppUpdate ReduceIntegration(AppState state, IntegrationCommand command) =>
        ReduceInTab(state, command.TabId, command, static (_, tab, c) => IntegrationReducer.Reduce(
            c,
            new IntegrationContext(
                tab.Id,
                tab.Location,
                tab.Head,
                tab.Operation,
                OperationBannerBuilder.ConflictCount(tab.WorkingTree),
                IsBusy(tab, RunningOperationKind.Integration))));

    private static AppUpdate ReduceRemote(AppState state, RemoteCommand command) =>
        ReduceInTab(state, command.TabId, command, static (s, tab, c) => RemoteReducer.Reduce(
            c,
            new RemoteContext(
                tab.Id,
                tab.Location,
                tab.Refs,
                s.Workspace.ActiveTabId == tab.Id,
                tab.Running.IsRunning(RunningOperationKind.Remotes),
                tab.Running.Excludes(RunningOperationKind.Remotes))));

    private static AppUpdate ReduceStash(AppState state, StashCommand command) =>
        ReduceInTab(state, command.TabId, command, static (_, tab, c) => StashReducer.Reduce(
            c,
            new StashContext(
                tab.Id,
                tab.Location,
                tab.Stashes,
                tab.View.FocusedPane,
                IsBusy(tab, RunningOperationKind.Stash))));

    private static AppUpdate ReduceTag(AppState state, TagCommand command) =>
        ReduceInTab(state, command.TabId, command, static (s, tab, c) => TagReducer.Reduce(
            c,
            new TagContext(
                tab.Id,
                tab.Location,
                tab.Refs,
                s.Workspace.ActiveTabId == tab.Id,
                IsBusy(tab, RunningOperationKind.Tags))));

    private static AppUpdate ReduceSync(AppState state, RepositoryState tab, SyncCommand command)
    {
        var shell = state.Shell;
        var overlay = shell.Confirm is not null || shell.Prompt is not null || shell.Output is not null;
        var busy = tab.Running.IsRunning(RunningOperationKind.Sync);
        var other = tab.Running.Excludes(RunningOperationKind.Sync);
        var context = new SyncContext(
            tab.Id,
            tab.Location,
            tab.Refs,
            state.Workspace.ActiveTabId == tab.Id,
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

    private static AppUpdate ReduceTabRename(AppState state, RepositoryState tab, TabRenameCommand command)
    {
        var update = TabRenameReducer.Reduce(tab, command);

        return new AppUpdate(WithTab(state, update.Tab), update.Effects);
    }

    // After a success the graph cursor follows to the commit the reducer named; the saved selection is the commit the refreshed graph then selects.
    private static AppUpdate ReduceBranch(AppState state, RepositoryState tab, BranchCommand command)
    {
        var busy = IsBusy(tab, RunningOperationKind.Branches);
        var context = new BranchContext(tab.Id, tab.Location, tab.Refs, tab.Operation, busy);
        var effects = BranchReducer.Reduce(command, context);
        var next = ApplyRunning(state, tab, command, effects);
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

    // A located worktree or a read overview is applied to the workspace after the feature has decided.
    private static AppUpdate ReduceWorktree(AppState state, RepositoryState tab, WorktreeCommand command)
    {
        var busy = tab.Running.IsRunning(RunningOperationKind.Worktrees);
        var update = WorktreeReducer.Reduce(command, new WorktreeContext(tab, state.Workspace.Tabs, busy));
        var next = ApplyRunning(state, tab, command, update.Effects);
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

    // The one tab lookup of the tab features: a command for a tab that was closed meanwhile is dropped, like a late
    // TabCommand. The lambdas are static, so nothing is allocated per command.
    private static AppUpdate InTab<TCommand>(
        AppState state,
        TabId tabId,
        TCommand command,
        Func<AppState, RepositoryState, TCommand, AppUpdate> reduce)
        where TCommand : AppCommand =>
        state.Workspace.FindTab(tabId) is { } tab ? reduce(state, tab, command) : new AppUpdate(state, []);

    private static AppUpdate ReduceInTab<TCommand>(
        AppState state,
        TabId tabId,
        TCommand command,
        Func<AppState, RepositoryState, TCommand, ImmutableArray<AppEffect>> reduce)
        where TCommand : AppCommand
    {
        if (state.Workspace.FindTab(tabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var effects = reduce(state, tab, command);

        return new AppUpdate(ApplyRunning(state, tab, command, effects), effects);
    }

    // Marks the operations the effects start and clears the one the result command ends; the state is returned
    // unchanged (same reference) when nothing moved.
    private static AppState ApplyRunning(
        AppState state,
        RepositoryState tab,
        AppCommand command,
        ImmutableArray<AppEffect> effects)
    {
        var running = tab.Running.Apply(tab.Id, command, effects);

        return Equals(running, tab.Running) ? state : WithTab(state, tab with { Running = running });
    }

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
