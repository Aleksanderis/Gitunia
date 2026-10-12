using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Workspaces;

/// <summary>Pure transitions of one tab's state.</summary>
public static class RepoReducer
{
    /// <summary>Applies a command to one tab's state.</summary>
    public static RepoState Reduce(RepoState state, RepoCommand command) => command switch
    {
        SelectCommitCommand select => state with { View = state.View with { SelectedCommit = select.Commit } },
        ScrollToCommand scroll => state with { View = state.View with { ScrollOffset = Math.Max(0, scroll.Offset) } },
        SetFilterCommand filter => state with { View = state.View with { FilterText = filter.Text } },
        FocusPaneCommand focus => state with { View = state.View with { FocusedPane = focus.Pane } },
        HeadLoadedCommand head => state with { Head = head.Head },
        RefsLoadedCommand refs => state with { Refs = refs.Refs },
        WorktreesLoadedCommand worktrees => state with { Worktrees = worktrees.Worktrees },
        StashesLoadedCommand stashes => state with { Stashes = stashes.Stashes },
        SidebarCommand sidebar => SidebarReducer.Reduce(state, sidebar),
        SetMissingCommand missing => state with { IsMissing = missing.IsMissing },
        _ => state,
    };
}
