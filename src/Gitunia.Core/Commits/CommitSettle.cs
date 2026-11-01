using Gitunia.Core.Editing;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Commits;

/// <summary>What must hold after every command, whoever caused it: Insert mode ends when the box that owned it lost the
/// focus (a click, <c>Tab</c> or a tab switch), and the output of a commit that failed in the background opens once its
/// tab is shown.</summary>
public static class CommitSettle
{
    /// <summary>Applies both rules to the state after a command.</summary>
    /// <param name="state">The state after a command.</param>
    public static AppState Settle(AppState state)
    {
        var active = state.Workspace.ActiveTab;
        var current = state;
        foreach (var tab in state.Workspace.Tabs)
        {
            if (tab.Commit.IsInserting)
            {
                current = LeaveInsert(current, tab, active);
            }
        }

        if (active is { Commit.PendingFailure: { } lines })
        {
            current = ShowPending(current, active, lines);
        }

        return current;
    }

    // The box owned Insert mode: it keeps it only while it is focused in the active tab and the mode is still Insert.
    private static AppState LeaveInsert(AppState state, RepositoryState tab, RepositoryState? active)
    {
        var current = state.Workspace.FindTab(tab.Id) ?? tab;
        var isInBox = active is { View: { Mode: TabViewMode.WorkingTree, FocusedPane: PaneKind.WorkingMessage } }
            && active.Id == tab.Id;
        if (isInBox && state.Shell.Mode == KeyMode.Insert)
        {
            return state;
        }

        var draft = current.Commit.Draft?.Clone();
        draft?.Editor.DropUndoPointIfUnchanged();
        var left = current with { Commit = current.Commit with { Draft = draft, IsInserting = false } };
        var shell = state.Shell;
        if (!isInBox && shell.Mode == KeyMode.Insert)
        {
            shell = shell.WithMode(KeyMode.Normal);
        }

        return state with { Workspace = Replace(state.Workspace, left), Shell = shell };
    }

    private static AppState ShowPending(AppState state, RepositoryState active, IReadOnlyList<string> lines)
    {
        var cleared = active with { Commit = active.Commit with { PendingFailure = null } };
        var shown = ShellReducer.Reduce(
            state.Shell,
            new OutputShownCommand(CommitFailureReport.Title, CommitFailureReport.Heading, lines));

        return state with { Workspace = Replace(state.Workspace, cleared), Shell = shown.State };
    }

    private static Workspace Replace(Workspace workspace, RepositoryState tab)
    {
        var tabs = workspace.Tabs;
        for (var index = 0; index < tabs.Length; index++)
        {
            if (tabs[index].Id == tab.Id)
            {
                return workspace with { Tabs = tabs.SetItem(index, tab) };
            }
        }

        return workspace;
    }
}
