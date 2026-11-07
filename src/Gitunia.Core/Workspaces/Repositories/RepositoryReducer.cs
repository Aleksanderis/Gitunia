using Gitunia.Core.Details;
using Gitunia.Core.DiffPane;
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
        SetWorkingListWidthCommand list => state with { View = state.View with { WorkingListWidth = list.Width } },
        SetDetailTreeCommand tree => state with { View = state.View with { FileTree = tree.Tree } },
        SetDiffPaneCommand diff => state with { View = state.View with { Diff = diff.Pane } },
        SetBottomTabCommand bottomTab => state with { View = state.View with { BottomTab = bottomTab.Tab } },
        SetCompareMarkCommand mark => state with { View = state.View with { CompareMark = mark.Commit } },
        TogglePathStyleCommand => WithStyle(
            state,
            state.View.PathStyle switch
            {
                FilePathStyle.NameFirst => FilePathStyle.FullPath,
                FilePathStyle.FullPath => FilePathStyle.Tree,
                _ => FilePathStyle.NameFirst,
            }),
        SetPathStyleCommand set => WithStyle(state, set.Style),
        ToggleWorkingFolderCommand folder => ToggleFolder(state, folder),
        HeadLoadedCommand head => state with { Head = head.Head },
        RefsLoadedCommand refs => state with { Refs = refs.Refs },
        StatusLoadedCommand status => state with { Status = status.Summary },
        WorkingTreeLoadedCommand loaded => ReduceWorkingTree(state, loaded),
        SelectWorkingTreeFileCommand pick => SelectFile(state, pick),
        WorktreesLoadedCommand worktrees => state with { Worktrees = worktrees.Worktrees },
        WorktreeStatusesLoadedCommand statuses => state with { WorktreeStatuses = statuses.Statuses },
        StashesLoadedCommand stashes => state with { Stashes = stashes.Stashes },
        OperationLoadedCommand operation => state with { Operation = operation.Operation },
        RebaseAutoStashLoadedCommand autoStash => state with { RebaseAutoStash = autoStash.IsOn },
        SidebarCommand sidebar => SidebarReducer.Reduce(state, sidebar),
        SetTabNameCommand rename => state with { CustomName = RepositoryState.NormalizeName(rename.Name) },
        SetMissingCommand missing => state with { IsMissing = missing.IsMissing },
        _ => state,
    };

    private static RepositoryState WithStyle(RepositoryState state, FilePathStyle style)
    {
        var toggled = state with { View = state.View with { PathStyle = style } };

        if (state.WorkingTree is not { } tree)
        {
            return toggled;
        }

        return toggled with { WorkingTree = tree.WithLayout(style, tree.CollapsedUnstaged, tree.CollapsedStaged) };
    }

    private static RepositoryState ToggleFolder(RepositoryState state, ToggleWorkingFolderCommand command)
    {
        if (state.WorkingTree is not { } tree)
        {
            return state;
        }

        var isUnstaged = command.Section == WorkingTreeSection.Unstaged;
        var current = tree.CollapsedOf(command.Section);
        var collapse = command.Collapse ?? !current.Contains(command.Folder);
        var changed = collapse ? current.Add(command.Folder) : current.Remove(command.Folder);

        return state with
        {
            WorkingTree = tree.WithLayout(
                tree.PathStyle,
                isUnstaged ? changed : tree.CollapsedUnstaged,
                isUnstaged ? tree.CollapsedStaged : changed),
        };
    }

    private static RepositoryState ReduceWorkingTree(RepositoryState state, WorkingTreeLoadedCommand loaded)
    {
        if (state.WorkingTree is { } current && WorkingTreeState.SameContent(current.Snapshot, loaded.Snapshot))
        {
            var summary = RepositoryStatusSummary.From(loaded.Snapshot);
            return state.Status == summary ? state : state with { Status = summary };
        }

        var reloaded = state with
        {
            WorkingTree = WorkingTreeState.Reconcile(state.WorkingTree, loaded.Snapshot, state.View.PathStyle),
            Status = RepositoryStatusSummary.From(loaded.Snapshot),
        };
        return FollowSelectionWhenFocusedListIsEmpty(reloaded);
    }

    // The focused list ran empty (the last file was staged or unstaged): focus goes where the selection went. A diff follows
    // its file the same way (the file moved to the other side), and with no file left focus returns to the Unstaged list.
    private static RepositoryState FollowSelectionWhenFocusedListIsEmpty(RepositoryState state)
    {
        if (state.View.Mode != TabViewMode.WorkingTree || state.WorkingTree is not { } tree)
        {
            return state;
        }

        var focused = state.View.FocusedPane;
        if (tree.Selection is not { } selection)
        {
            return WorkingPanes.IsDiff(focused) ? state with { View = state.View with { FocusedPane = PaneKind.WorkingUnstaged } } : state;
        }

        var isFocusedListEmpty = focused switch
        {
            PaneKind.WorkingUnstaged => tree.Unstaged.Count == 0,
            PaneKind.WorkingStaged => tree.Staged.Count == 0,
            _ => false,
        };
        if (isFocusedListEmpty)
        {
            return state with { View = state.View with { FocusedPane = PaneOf(selection.Section) } };
        }

        var diffOfSelection = WorkingPanes.DiffOf(selection.Section);
        return WorkingPanes.IsDiff(focused) && focused != diffOfSelection ? state with { View = state.View with { FocusedPane = diffOfSelection } } : state;
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
        if (WorkingPanes.IsDiff(pane))
        {
            // A diff shows the selected file: focus the side that file is on, and none when nothing is selected.
            if (state.WorkingTree?.Selection is not { } selected)
            {
                return state;
            }

            return state with { View = state.View with { FocusedPane = WorkingPanes.DiffOf(selected.Section) } };
        }

        var focused = state with { View = state.View with { FocusedPane = pane } };
        if (pane is not (PaneKind.WorkingUnstaged or PaneKind.WorkingStaged) || state.WorkingTree is not { } tree)
        {
            return focused;
        }

        var section = pane == PaneKind.WorkingUnstaged ? WorkingTreeSection.Unstaged : WorkingTreeSection.Staged;
        if (tree.Selection?.Section == section)
        {
            return focused;
        }

        return SelectFile(focused, new SelectWorkingTreeFileCommand(section, tree.Selection?.Index ?? 0));
    }

    private static PaneKind PaneOf(WorkingTreeSection section) =>
        section == WorkingTreeSection.Unstaged ? PaneKind.WorkingUnstaged : PaneKind.WorkingStaged;

    private static RepositoryState SelectFile(RepositoryState state, SelectWorkingTreeFileCommand pick)
    {
        if (state.WorkingTree is not { } tree)
        {
            return state;
        }

        var count = tree.RowsOf(pick.Section).Count;
        if (count == 0)
        {
            return state;
        }

        var selection = tree.SelectionAt(pick.Section, Math.Clamp(pick.Index, 0, count - 1));

        return state with { WorkingTree = tree with { Selection = selection } };
    }
}
