namespace Gitunia.Core.Sidebar;

/// <summary>What a sidebar row is.</summary>
public enum SidebarRowKind
{
    /// <summary>A section header that expands and collapses.</summary>
    Section,
    LocalBranch,
    RemoteBranch,
    Tag,
    Worktree,
    Stash,
}
