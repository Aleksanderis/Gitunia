using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Pure transitions of one tab's state.</summary>
public static class RepositoryReducer
{
    /// <summary>Applies a command to one tab's state.</summary>
    public static RepositoryState Reduce(RepositoryState state, RepositoryCommand command) => command switch
    {
        SelectCommitCommand select => state with { View = state.View with { SelectedCommit = select.Commit } },
        ScrollToCommand scroll => state with { View = state.View with { ScrollOffset = Math.Max(0, scroll.Offset) } },
        SetFilterCommand filter => state with { View = state.View with { FilterText = filter.Text } },
        FocusPaneCommand focus => FocusPane(state, focus.Pane),
        OpenWorkingTreeCommand => OpenWorkingTree(state),
        LeaveWorkingTreeCommand => state with { View = state.View with { Mode = TabViewMode.History, FocusedPane = PaneKind.Graph } },
        SetPaneSizesCommand sizes => state with { View = state.View with { SidebarWidth = sizes.SidebarWidth, BottomHeight = sizes.BottomHeight, DetailWidth = sizes.DetailWidth } },
        SetBottomTabCommand bottomTab => state with { View = state.View with { BottomTab = bottomTab.Tab } },
        HeadLoadedCommand head => state with { Head = head.Head },
        RefsLoadedCommand refs => state with { Refs = refs.Refs },
        StatusLoadedCommand status => state with { Status = status.Summary },
        WorkingTreeLoadedCommand loaded => ReduceWorkingTree(state, loaded),
        SelectWorkingTreeFileCommand pick => SelectFile(state, pick),
        WorktreesLoadedCommand worktrees => state with { Worktrees = worktrees.Worktrees },
        StashesLoadedCommand stashes => state with { Stashes = stashes.Stashes },
        SidebarCommand sidebar => SidebarReducer.Reduce(state, sidebar),
        SetTabNameCommand rename => state with { CustomName = RepositoryState.NormalizeName(rename.Name) },
        SetMissingCommand missing => state with { IsMissing = missing.IsMissing },
        _ => state,
    };

    private static RepositoryState ReduceWorkingTree(RepositoryState state, WorkingTreeLoadedCommand loaded)
    {
        if (state.WorkingTree is { } current && WorkingTreeState.SameContent(current.Snapshot, loaded.Snapshot))
        {
            var summary = RepositoryStatusSummary.From(loaded.Snapshot);
            return state.Status == summary ? state : state with { Status = summary };
        }

        var reloaded = state with
        {
            WorkingTree = WorkingTreeState.Reconcile(state.WorkingTree, loaded.Snapshot),
            Status = RepositoryStatusSummary.From(loaded.Snapshot),
        };
        return FollowSelectionWhenFocusedListIsEmpty(reloaded);
    }

    // The focused list ran empty (the last file was staged or unstaged): focus goes where the selection went.
    private static RepositoryState FollowSelectionWhenFocusedListIsEmpty(RepositoryState state)
    {
        if (state.View.Mode != TabViewMode.WorkingTree || state.WorkingTree is not { Selection: { } selection } tree)
        {
            return state;
        }

        var focused = state.View.FocusedPane;
        var isFocusedListEmpty = focused switch
        {
            PaneKind.WorkingUnstaged => tree.Unstaged.Count == 0,
            PaneKind.WorkingStaged => tree.Staged.Count == 0,
            _ => false,
        };
        return isFocusedListEmpty ? state with { View = state.View with { FocusedPane = PaneOf(selection.Section) } } : state;
    }

    private static RepositoryState OpenWorkingTree(RepositoryState state)
    {
        var section = state.WorkingTree is { Unstaged.Count: 0, Staged.Count: > 0 } ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        var opened = state with { View = state.View with { Mode = TabViewMode.WorkingTree, FocusedPane = PaneOf(section) } };
        return FocusPane(opened, PaneOf(section));
    }

    // Focusing a list moves the selection into it, on the row the cursor was on (clamped), so the cursor and the focus never disagree.
    private static RepositoryState FocusPane(RepositoryState state, PaneKind pane)
    {
        var focused = state with { View = state.View with { FocusedPane = pane } };
        if (pane is not (PaneKind.WorkingUnstaged or PaneKind.WorkingStaged) || state.WorkingTree is not { } tree)
        {
            return focused;
        }

        var section = pane == PaneKind.WorkingUnstaged ? WorkingTreeSection.Unstaged : WorkingTreeSection.Staged;
        return tree.Selection?.Section == section ? focused : SelectFile(focused, new SelectWorkingTreeFileCommand(section, tree.Selection?.Index ?? 0));
    }

    private static PaneKind PaneOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? PaneKind.WorkingUnstaged : PaneKind.WorkingStaged;

    private static RepositoryState SelectFile(RepositoryState state, SelectWorkingTreeFileCommand pick)
    {
        if (state.WorkingTree is not { } tree)
        {
            return state;
        }

        var list = pick.Section == WorkingTreeSection.Unstaged ? tree.Unstaged : tree.Staged;
        if (list.Count == 0)
        {
            return state;
        }

        var index = Math.Clamp(pick.Index, 0, list.Count - 1);
        return state with { WorkingTree = tree with { Selection = new WorkingTreeSelection(pick.Section, list[index].Path, index) } };
    }
}
