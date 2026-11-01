using System.Collections.Frozen;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Commits;

/// <summary>Questions about the message boxes that views, the session file and the quit guard ask. Pure.</summary>
public static class CommitRules
{
    private static readonly FrozenSet<ActionId> NormalKeys = FrozenSet.ToFrozenSet(
    [
        ActionIds.WorkingTreeMessageLeft,
        ActionIds.WorkingTreeMessageDown,
        ActionIds.WorkingTreeMessageUp,
        ActionIds.WorkingTreeMessageRight,
        ActionIds.WorkingTreeMessageWordNext,
        ActionIds.WorkingTreeMessageWordPrevious,
        ActionIds.WorkingTreeMessageLineStart,
        ActionIds.WorkingTreeMessageLineEnd,
        ActionIds.WorkingTreeMessageAppend,
        ActionIds.WorkingTreeMessageAppendAtEnd,
        ActionIds.WorkingTreeMessageInsertAtStart,
        ActionIds.WorkingTreeMessageOpenLine,
        ActionIds.WorkingTreeMessageDeleteChar,
        ActionIds.WorkingTreeMessageDeleteLine,
        ActionIds.WorkingTreeMessageUndo,
    ]);

    /// <summary>True for the Normal mode keys of the box.</summary>
    /// <param name="action">The action.</param>
    public static bool IsNormalKey(ActionId action) => NormalKeys.Contains(action);

    /// <summary>True for the actions the message box runs whatever the state is.</summary>
    /// <param name="action">The action.</param>
    public static bool Handles(ActionId action) =>
        action == ActionIds.WorkingTreeWriteMessage
        || action == ActionIds.WorkingTreeEditMessage
        || action == ActionIds.WorkingTreeMessageNewLine
        || NormalKeys.Contains(action)
        || action == ActionIds.WorkingTreeCommit
        || action == ActionIds.WorkingTreeToggleSignOff
        || action == ActionIds.WorkingTreeToggleNoVerify
        || action == ActionIds.WorkingTreeToggleAmend;

    /// <summary>Whether the message box takes the action in this state, so no other handler may have it (Esc and Back
    /// are the box's only while a commit runs, while it is edited or while it has the focus).</summary>
    /// <param name="state">The state.</param>
    /// <param name="tab">The active tab, showing the Changes view.</param>
    /// <param name="action">The action.</param>
    public static bool Takes(AppState state, RepositoryState tab, ActionId action)
    {
        var inBox = tab.View.FocusedPane == PaneKind.WorkingMessage;
        if ((action == ActionIds.Cancel || action == ActionIds.NavigateBack)
            && tab.Running.IsRunning(RunningOperationKind.Commit))
        {
            return true;
        }

        if (action == ActionIds.Cancel)
        {
            return inBox && IsEditing(state, tab);
        }

        return action == ActionIds.NavigateBack ? inBox : Handles(action);
    }

    /// <summary>True while the message box of the active tab takes typed keys.</summary>
    /// <param name="state">The state.</param>
    public static bool IsEditing(AppState state) =>
        state.Workspace.ActiveTab is { } tab && IsEditing(state, tab);

    /// <summary>True while the box of <paramref name="tab"/> takes typed keys.</summary>
    /// <param name="state">The state.</param>
    /// <param name="tab">The tab.</param>
    public static bool IsEditing(AppState state, RepositoryState tab) =>
        IsEditing(tab, state.Workspace.ActiveTabId == tab.Id, state.Shell.Mode);

    /// <summary>True while the box of <paramref name="tab"/> takes typed keys.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="isActive">The tab is the active one.</param>
    /// <param name="mode">The key mode.</param>
    public static bool IsEditing(RepositoryState tab, bool isActive, KeyMode mode) =>
        tab.Commit.IsInserting
        && isActive
        && mode == KeyMode.Insert
        && tab.View.FocusedPane == PaneKind.WorkingMessage;

    /// <summary>True while the focused message box shows its cursor: Insert mode, and the box's Normal mode.</summary>
    /// <param name="state">The state.</param>
    public static bool ShowsCursor(AppState state) =>
        IsEditing(state)
        || (state.Workspace.ActiveTab is { View: { Mode: TabViewMode.WorkingTree, FocusedPane: PaneKind.WorkingMessage } }
            && state.Shell.Mode == KeyMode.Normal);

    /// <summary>True when the tab holds a message that was typed or edited and not committed.</summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="tabId">The tab.</param>
    public static bool HasUnsentMessage(Workspace workspace, TabId tabId) =>
        workspace.FindTab(tabId)?.Commit.HasUnsentText == true;

    /// <summary>Whether any tab holds an unsent message.</summary>
    /// <param name="workspace">The workspace.</param>
    public static bool HasAnyUnsentMessage(Workspace workspace) => workspace.Tabs.Any(tab => tab.Commit.HasUnsentText);

    /// <summary>The drafts worth keeping across a restart (message, SignOff, No-Verify), by tab.</summary>
    /// <param name="workspace">The workspace.</param>
    public static IReadOnlyDictionary<TabId, SessionDraft> SessionDrafts(Workspace workspace)
    {
        var result = new Dictionary<TabId, SessionDraft>();
        foreach (var tab in workspace.Tabs)
        {
            if (tab.Commit.Draft is { } draft && SessionOf(draft) is { IsEmpty: false } saved)
            {
                result[tab.Id] = saved;
            }
        }

        return result;
    }

    /// <summary>
    /// What a restart (or a move to the tab that replaces a missing one) keeps of a draft: never Amend, which belongs to the
    /// old HEAD, so an amend message (HEAD's, maybe edited) must not come back as a plain new draft.
    /// </summary>
    /// <param name="draft">The draft.</param>
    public static SessionDraft SessionOf(CommitDraft draft)
    {
        var text = draft.HasUnsentText && !draft.Amend ? draft.Editor.Text : string.Empty;

        return new SessionDraft(text, draft.SignOff, draft.NoVerify);
    }

    /// <summary>The list that gets the focus back when the box is left: the one with the selection.</summary>
    /// <param name="tab">The tab.</param>
    public static PaneKind ListPaneOf(RepositoryState tab) =>
        tab.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged
            ? PaneKind.WorkingStaged
            : PaneKind.WorkingUnstaged;
}
