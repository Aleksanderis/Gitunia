namespace Gitunia.Core.Workspaces.Panes;

/// <summary>The panes of a repository tab, in focus-cycling order.</summary>
public enum PaneKind
{
    Sidebar,
    Graph,
    Detail,
    Diff,

    /// <summary>The Unstaged file list of the Changes view (that view only).</summary>
    WorkingUnstaged,

    /// <summary>The Staged file list of the Changes view (that view only).</summary>
    WorkingStaged,

    /// <summary>The diff of the file selected in the Unstaged list (that view only).</summary>
    WorkingUnstagedDiff,

    /// <summary>The diff of the file selected in the Staged list (that view only).</summary>
    WorkingStagedDiff,

    /// <summary>The commit message box of the Changes view (that view only).</summary>
    WorkingMessage,
}
