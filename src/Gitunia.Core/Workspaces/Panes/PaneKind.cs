namespace Gitunia.Core.Workspaces.Panes;

/// <summary>The panes of a repository tab, in focus-cycling order.</summary>
public enum PaneKind
{
    Sidebar,
    Graph,
    Detail,
    Diff,

    /// <summary>The Unstaged file list of the working-tree view (that view only).</summary>
    WorkingUnstaged,

    /// <summary>The Staged file list of the working-tree view (that view only).</summary>
    WorkingStaged,

    /// <summary>The commit message box of the working-tree view (that view only).</summary>
    WorkingMessage,
}
