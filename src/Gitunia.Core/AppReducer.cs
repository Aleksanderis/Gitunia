using Gitunia.Core.Branches;
using Gitunia.Core.Commits;
using Gitunia.Core.Compare;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Creation;
using Gitunia.Core.Details;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Discard;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Picker;
using Gitunia.Core.Effects;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Input;
using Gitunia.Core.IssueLinks;
using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Pick;
using Gitunia.Core.Integration;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Remotes;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Stash;
using Gitunia.Core.Running;
using Gitunia.Core.Sync;
using Gitunia.Core.Tags;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;

namespace Gitunia.Core;

/// <summary>
/// The root reducer <c>(AppState, AppCommand) -> (AppState, Effects)</c>: pure, no I/O, no clock. A case here holds no
/// logic; it delegates to the feature's reducer. Cases are in alphabetical order by feature.
/// </summary>
public static partial class AppReducer
{
    /// <summary>Applies a command and returns the new state and the effects to run.</summary>
    public static AppUpdate Reduce(AppState state, AppCommand command)
    {
        var update = ReduceCommand(state, command);
        var settled = DiffPaneReducer.Settle(CommitSettle.Settle(update.State));

        // Settling may move the focus too, so the range is checked last.
        var ranged = VisualReducer.EndIfFocusMoved(settled.State);
        if (settled.Effects.IsEmpty && ReferenceEquals(ranged, update.State))
        {
            return update;
        }

        return new AppUpdate(ranged, [.. update.Effects, .. settled.Effects]);
    }

    private static AppUpdate ReduceCommand(AppState state, AppCommand command) => command switch
    {
        BranchCommand branch => InTab(state, branch.TabId, branch, ReduceBranch),
        CommitCommand commit => InTab(state, commit.TabId, commit, ReduceCommit),
        CommitDraftMoveRequested move => ReduceCommitDraftMove(state, move),
        CompareCommand compare => InTab(state, compare.TabId, compare, ReduceCompare),
        ConflictCommand conflict => ReduceConflict(state, conflict),
        CreationCommand creation => ReduceCreation(state, creation),
        DetailCommand detail => DetailReducer.Reduce(state, detail),
        DiffPaneCommand diffPane => DiffPaneReducer.Reduce(state, diffPane),
        DiscardCommand discard => InTab(state, discard.TabId, discard, ReduceDiscard),
        EffectFailedCommand failed => EffectFailed(state, failed),
        ExternalToolCommand tool => new AppUpdate(state, ExternalToolReducer.Reduce(tool, state.Workspace.ActiveTab)),
        InputCommand input => InputReducer.Reduce(state, input),
        IssueLinkCommand link => new AppUpdate(state, IssueLinkReducer.Reduce(link)),
        PaneSizeCommand paneSize => PaneSizeReducer.Reduce(state, paneSize),
        PickCommand pick => InTab(state, pick.TabId, pick, ReducePick),
        RebaseCommand rebase => ReduceRebase(state, rebase),
        RebaseEditorCommand editor => ReduceRebaseEditor(state, editor),
        IntegrationCommand integration => ReduceIntegration(state, integration),
        PickerCommand picker => ReducePicker(state, picker),
        QuickJumpCommand jump => ReduceQuickJump(state, jump),
        QuitCommand quit => ReduceQuit(state, quit),
        ReflogCommand reflog => ReduceReflog(state, reflog),
        SettingsCommand settings => ReduceSettings(state, settings),
        RemoteCommand remote => ReduceRemote(state, remote),
        ShellCommand shell => ReduceShell(state, shell),
        SidebarActionCommand sidebar => InTab(state, sidebar.TabId, sidebar, ReduceSidebarAction),
        StagingCommand staging => InTab(state, staging.TabId, staging, ReduceStaging),
        StashCommand stash => ReduceStash(state, stash),
        SyncCommand sync => InTab(state, sync.TabId, sync, ReduceSync),
        TabRenameCommand rename => InTab(state, rename.TabId, rename, ReduceTabRename),
        TagCommand tag => ReduceTag(state, tag),
        VisualCommand visual => VisualReducer.Reduce(state, visual),
        WorkspaceActionPressed action => ReduceWorkspaceAction(state, action),
        WorkspaceCommand workspace => ReduceWorkspace(state, workspace),
        WorktreeCommand worktree => InTab(state, worktree.TabId, worktree, ReduceWorktree),
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
        var shell = EndFilterOutsideTab(state.Shell, update.Workspace);
        if (shell.Mode == KeyMode.Insert && CommitSettle.OwnerClosed(state.Workspace, update.Workspace))
        {
            shell = shell.WithMode(KeyMode.Normal);
        }

        if (shell.Prompt is { Owner: { } owner } && update.Workspace.ActiveTabId != owner)
        {
            shell = ShellReducer.Reduce(shell, new PromptCancelledCommand()).State;
        }

        if (shell.Mainline is { } question && update.Workspace.ActiveTabId != question.TabId)
        {
            shell = (shell with { Mainline = null }).WithMode(KeyMode.Normal);
        }

        List<AppEffect> effects = [.. update.Effects];
        if (shell.RebaseEditor.Editor is { } todo && update.Workspace.FindTab(todo.TabId) is null)
        {
            var closedTodo = RebaseEditorReducer.Closed(shell.RebaseEditor);
            shell = shell with { RebaseEditor = closedTodo.Session };
            effects.AddRange(closedTodo.Effects);
        }

        var changed = DetailReducer.CloseFilterOfLeftTab(state.Workspace, new AppState(update.Workspace, shell));
        shell = changed.Shell;
        if (shell.Reflog.List is { } list && update.Workspace.FindTab(list.TabId) is null)
        {
            var closed = ReduceReflog(changed, new ReflogTabClosed());

            return new AppUpdate(closed.State, [.. effects, .. closed.Effects]);
        }

        if (shell.Conflict.Editor is { } editor && update.Workspace.FindTab(editor.TabId) is null)
        {
            var closed = ReduceConflict(changed, new ConflictTabClosed());

            return new AppUpdate(closed.State, [.. effects, .. closed.Effects]);
        }

        return new AppUpdate(changed, [.. effects]);
    }

    // The editor belongs to the tab it was opened in. A result for a tab that was closed meanwhile reaches the reducer
    // without a tab: it reports what is left to report and the running mark is gone with the tab.
    private static AppUpdate ReduceConflict(AppState state, ConflictCommand command)
    {
        var workspace = state.Workspace;
        var session = state.Shell.Conflict;
        var tab = ConflictTabOf(workspace, session, command);
        var covered = IsOverlayOpen(state.Shell with { Conflict = ConflictSession.Empty });
        var busy = tab is not null && IsBusy(tab, RunningOperationKind.Conflicts);
        var update = ConflictReducer.Reduce(session, command, new ConflictContext(tab, busy, covered));
        var next = state with { Shell = state.Shell with { Conflict = update.Session } };
        if (tab is not null)
        {
            next = ApplyRunning(next, tab, command, update.Effects);
        }

        return new AppUpdate(next, update.Effects);
    }

    private static RepositoryState? ConflictTabOf(Workspace workspace, ConflictSession session, ConflictCommand command)
    {
        if (command is ConflictTabCommand tabCommand)
        {
            return workspace.FindTab(tabCommand.TabId);
        }

        return session.Editor is { } editor ? workspace.FindTab(editor.TabId) : workspace.ActiveTab;
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

    // The mainline question lives in the shell state because it is an overlay; the reducer returns it after each
    // command.
    private static AppUpdate ReducePick(AppState state, RepositoryState tab, PickCommand command)
    {
        var busy = IsBusy(tab, RunningOperationKind.Pick);
        var question = state.Shell.Mainline is { } open && open.TabId == tab.Id ? open : null;
        var context = new PickContext(
            tab.Id,
            tab.Location,
            tab.Head,
            tab.Operation,
            state.Workspace.ActiveTabId == tab.Id,
            busy,
            question);
        var update = PickReducer.Reduce(command, context);
        var next = ApplyRunning(state, tab, command, update.Effects);

        if (!Equals(update.Question, question))
        {
            next = next with { Shell = next.Shell with { Mainline = update.Question } };
        }

        return new AppUpdate(next, update.Effects);
    }

    private static AppUpdate ReduceRebase(AppState state, RebaseCommand command) =>
        ReduceInTab(state, command.TabId, command, static (s, tab, c) => RebaseReducer.Reduce(
            c,
            new RebaseContext(
                tab,
                s.Workspace.ActiveTabId == tab.Id,
                IsBusy(tab, RunningOperationKind.Rebase),
                IsOverlayOpen(s.Shell))));

    // A result or answer for a tab that was closed meanwhile is dropped, like a late TabCommand; a read result only
    // ends the read, or the next one would be refused for good.
    private static AppUpdate ReduceRebaseEditor(AppState state, RebaseEditorCommand command)
    {
        var workspace = state.Workspace;
        var session = state.Shell.RebaseEditor;
        if (workspace.FindTab(command.TabId) is not { } tab)
        {
            var ended = command is RebaseEditorReadResult ? session with { IsReading = false } : session;

            return new AppUpdate(state with { Shell = state.Shell with { RebaseEditor = ended } }, []);
        }

        var busy = IsBusy(tab, RunningOperationKind.Rebase);
        var overlay = IsOverlayOpen(state.Shell with { RebaseEditor = RebaseEditorSession.Empty });
        var context = new RebaseEditorContext(tab, state.Workspace.ActiveTabId == tab.Id, busy, overlay);
        var update = RebaseEditorReducer.Reduce(session, command, context);
        var next = ApplyRunning(state, tab, command, update.Effects);

        if (!ReferenceEquals(update.Session, session))
        {
            next = next with { Shell = next.Shell with { RebaseEditor = update.Session } };
        }

        return new AppUpdate(next, update.Effects);
    }

    // A late question must not replace what the user is looking at.
    private static bool IsOverlayOpen(ShellState shell) =>
        shell.Confirm is not null
        || shell.Output is not null
        || shell.Prompt is not null
        || shell.Mainline is not null
        || shell.RebaseEditor.IsOpen
        || shell.Conflict.IsOpen
        || shell.Reflog.IsOpen
        || shell.Settings.IsOpen
        || shell.Overlay != OverlayKind.None;
}
