using System.Collections.Immutable;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git;

namespace Gitunia.Core.Sidebar;

/// <summary>
/// The actions that concern pane focus and the sidebar as the pure transition
/// <c>(SidebarSession, SidebarActionCommand, tab) -> SidebarActionUpdate</c>: moving between panes, the sidebar's own
/// keys, the name filter and jumping from a ref to its commit in the graph. The filter being typed is the session
/// (the root turns it into Insert mode); focus, cursor and filter text are commands for the tab.
/// </summary>
public static class SidebarActionReducer
{
    /// <summary>Shown when copy is pressed with no rows.</summary>
    public const string NothingToCopyNotice = "Nothing to copy";

    /// <summary>Shown when copy is pressed on a section row.</summary>
    public const string SelectEntryNotice = "Select an entry to copy";

    /// <summary>Whose an action is: the pane keys always, the filter keys while typing, the list keys on focus.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, or null.</param>
    /// <param name="focusedPane">The focused pane of the active tab.</param>
    /// <param name="isEditingFilter">The filter of the active tab is being typed.</param>
    public static SidebarActionKind Classify(ActionId action, int? count, PaneKind focusedPane, bool isEditingFilter)
    {
        if (IsPaneAction(action))
        {
            return SidebarActionKind.Pane;
        }

        if (isEditingFilter)
        {
            return action == ActionIds.InputAccept || action == ActionIds.Cancel
                ? SidebarActionKind.Filter
                : SidebarActionKind.None;
        }

        return focusedPane == PaneKind.Sidebar && IsSidebarAction(action, count)
            ? SidebarActionKind.Sidebar
            : SidebarActionKind.None;
    }

    /// <summary>Applies a command for <paramref name="tab"/>.</summary>
    public static SidebarActionUpdate Reduce(SidebarSession session, SidebarActionCommand command, RepositoryState tab) =>
        command switch
        {
            SidebarActionPressed pressed => Pressed(session, pressed, tab),
            SidebarCommitLocated located => Effects(session, new SelectGraphIndexEffect(located.Index)),
            SidebarCommitMissing missing => Effects(session, Notice(MissingNotice(missing))),
            SidebarFilterKeyTyped typed => Typed(session, typed, tab),
            _ => throw new UnhandledCommandException(command),
        };

    /// <summary>The row under the sidebar cursor, null when the list is empty.</summary>
    public static SidebarRow? CursorRow(RepositoryState tab)
    {
        var rows = SidebarBuilder.Build(tab);

        return rows.IsEmpty ? null : rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rows.Length)];
    }

    private static bool IsPaneAction(ActionId action) =>
        action == ActionIds.PaneNext
        || action == ActionIds.PanePrevious
        || action == ActionIds.PaneFocusLeft
        || action == ActionIds.PaneFocusRight
        || action == ActionIds.PaneFocusUp
        || action == ActionIds.PaneFocusDown;

    private static bool IsSidebarAction(ActionId action, int? count) =>
        ListMotion.TryMove(action, count, 0, 1, 1, out _)
        || action == ActionIds.SidebarExpand
        || action == ActionIds.SidebarCollapse
        || action == ActionIds.SidebarActivate
        || action == ActionIds.SidebarCopy
        || action == ActionIds.SidebarFilter
        || action == ActionIds.NavigateBack
        || action == ActionIds.Cancel;

    private static SidebarActionUpdate Pressed(SidebarSession session, SidebarActionPressed pressed, RepositoryState tab)
    {
        var action = pressed.Action;
        var focused = tab.View.FocusedPane;
        var editing = session.IsEditing(tab.Id);
        return Classify(action, pressed.Count, focused, editing) switch
        {
            SidebarActionKind.Pane => Focus(session, PaneTarget(action, focused, pressed.OpenPanes)),
            SidebarActionKind.Filter => FilterEditing(action),
            SidebarActionKind.Sidebar => OnSidebar(session, pressed, tab),
            _ => new SidebarActionUpdate(session, [], []),
        };
    }

    private static PaneKind PaneTarget(ActionId action, PaneKind focused, ImmutableArray<PaneKind> open)
    {
        if (action == ActionIds.PaneNext || action == ActionIds.PanePrevious)
        {
            return PaneNavigator.Cycle(focused, action == ActionIds.PaneNext, open);
        }

        if (action == ActionIds.PaneFocusLeft || action == ActionIds.PaneFocusRight)
        {
            return PaneNavigator.Step(focused, action == ActionIds.PaneFocusRight, open);
        }

        return PaneNavigator.StepVertically(focused, action == ActionIds.PaneFocusDown, open);
    }

    private static SidebarActionUpdate FilterEditing(ActionId action)
    {
        if (action == ActionIds.Cancel)
        {
            return new SidebarActionUpdate(SidebarSession.Empty, [new SetSidebarFilterCommand(string.Empty)], []);
        }

        return new SidebarActionUpdate(SidebarSession.Empty, [], []);
    }

    private static SidebarActionUpdate OnSidebar(SidebarSession session, SidebarActionPressed pressed, RepositoryState tab)
    {
        var action = pressed.Action;
        if (ListMotion.TryMove(action, pressed.Count, 0, 1, 1, out _))
        {
            return new SidebarActionUpdate(
                session,
                [new MoveSidebarCursorCommand(action, pressed.Count, pressed.PageHeight)],
                [new FollowStashCursorEffect()]);
        }

        if (action == ActionIds.SidebarExpand)
        {
            return Tab(session, new ExpandSidebarCommand());
        }

        if (action == ActionIds.SidebarCollapse)
        {
            return Tab(session, new CollapseSidebarCommand());
        }

        if (action == ActionIds.SidebarActivate)
        {
            return Activate(session, tab);
        }

        if (action == ActionIds.SidebarCopy)
        {
            return Effects(session, Copy(tab));
        }

        if (action == ActionIds.SidebarFilter)
        {
            return new SidebarActionUpdate(new SidebarSession(tab.Id), [], []);
        }

        if (action == ActionIds.NavigateBack || (action == ActionIds.Cancel && !tab.Sidebar.HasFilter))
        {
            return Focus(session, PaneKind.Graph);
        }

        return Tab(session, new SetSidebarFilterCommand(string.Empty));
    }

    private static SidebarActionUpdate Typed(SidebarSession session, SidebarFilterKeyTyped typed, RepositoryState tab)
    {
        if (!session.IsEditing(tab.Id))
        {
            return new SidebarActionUpdate(session, [], []);
        }

        var text = tab.Sidebar.FilterText;
        var key = typed.Key;
        if (key.IsPrintable)
        {
            return Tab(session, new SetSidebarFilterCommand(text + key.Name));
        }

        if (key.Name == KeyNames.Backspace && text.Length > 0)
        {
            var length = text.Length > 1 && char.IsLowSurrogate(text[^1]) ? 2 : 1;

            return Tab(session, new SetSidebarFilterCommand(text[..^length]));
        }

        return new SidebarActionUpdate(session, [], []);
    }

    private static SidebarActionUpdate Activate(SidebarSession session, RepositoryState tab)
    {
        if (CursorRow(tab) is not { } row)
        {
            return new SidebarActionUpdate(session, [], []);
        }

        if (row.IsContainer)
        {
            return Tab(session, new ToggleSidebarSectionCommand());
        }

        if (row.Kind == SidebarRowKind.Stash)
        {
            return ShowStash(session, tab, row);
        }

        if (row.CommitHash is null || !ObjectId.TryParse(row.CommitHash, out var commit))
        {
            return new SidebarActionUpdate(
                session,
                [],
                [new LeaveStashEffect(), Notice($"{row.Label} has no commits yet")]);
        }

        return new SidebarActionUpdate(
            session,
            [],
            [new LeaveStashEffect(), new LocateGraphCommitEffect(tab.Id, commit, row.Label)]);
    }

    private static SidebarActionUpdate ShowStash(SidebarSession session, RepositoryState tab, SidebarRow row)
    {
        if (StashViewRules.Find(tab.Stashes, row.CommitHash) is { } entry && ObjectId.TryParse(entry.CommitHash, out _))
        {
            return Effects(session, new ShowStashEffect(row));
        }

        return Effects(session, Notice($"{row.Label} is no longer in the stash list"));
    }

    private static AppEffect Copy(RepositoryState tab)
    {
        if (CursorRow(tab) is not { } row)
        {
            return Notice(NothingToCopyNotice);
        }

        if (row.IsContainer)
        {
            return Notice(SelectEntryNotice);
        }

        if (row.Kind == SidebarRowKind.Worktree)
        {
            return new CopyToClipboardEffect(row.Path ?? row.Label, "worktree path");
        }

        var noun = row.Kind switch
        {
            SidebarRowKind.Tag => "tag name",
            SidebarRowKind.Stash => "stash name",
            _ => "branch name",
        };

        return new CopyToClipboardEffect(row.Label, noun);
    }

    private static string MissingNotice(SidebarCommitMissing missing)
    {
        // Past the soft row limit the commit may still come: the rest loads now, and the next try finds it.
        return missing.IsGraphComplete ? $"{missing.Label} is not in the graph" : $"{missing.Label} is not loaded yet";
    }

    private static SidebarActionUpdate Focus(SidebarSession session, PaneKind pane) =>
        Tab(session, new FocusPaneCommand(pane));

    private static SidebarActionUpdate Tab(SidebarSession session, RepositoryCommand command) =>
        new(session, [command], []);

    private static SidebarActionUpdate Effects(SidebarSession session, AppEffect effect) =>
        new(session, [], [effect]);

    private static PostStatusEffect Notice(string text) => new(text, StatusSeverity.Info);
}
