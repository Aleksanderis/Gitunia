using Gitunia.Core.Sidebar;
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
        FocusPaneCommand focus => state with { View = state.View with { FocusedPane = focus.Pane } },
        SetPaneSizesCommand sizes => state with { View = state.View with { SidebarWidth = sizes.SidebarWidth, BottomHeight = sizes.BottomHeight, DetailWidth = sizes.DetailWidth } },
        SetBottomTabCommand bottomTab => state with { View = state.View with { BottomTab = bottomTab.Tab } },
        HeadLoadedCommand head => state with { Head = head.Head },
        RefsLoadedCommand refs => state with { Refs = refs.Refs },
        StatusLoadedCommand status => state with { Status = status.Summary },
        WorktreesLoadedCommand worktrees => state with { Worktrees = worktrees.Worktrees },
        StashesLoadedCommand stashes => state with { Stashes = stashes.Stashes },
        SidebarCommand sidebar => SidebarReducer.Reduce(state, sidebar),
        SetTabNameCommand rename => state with { CustomName = RepositoryState.NormalizeName(rename.Name) },
        SetMissingCommand missing => state with { IsMissing = missing.IsMissing },
        _ => state,
    };
}
