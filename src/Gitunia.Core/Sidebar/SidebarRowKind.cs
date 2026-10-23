namespace Gitunia.Core.Sidebar;

/// <summary>What a sidebar row is.</summary>
public enum SidebarRowKind
{
    /// <summary>A section header that expands and collapses.</summary>
    Section,
    /// <summary>A prefix folder of the branch lists (<c>feature/</c>) that expands and collapses.</summary>
    Folder,
    LocalBranch,
    RemoteBranch,
    Tag,
    Worktree,
    Stash,
}
