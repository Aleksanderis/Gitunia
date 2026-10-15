namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Which view of the repository a tab shows (docs/ux.md, workflow 2).</summary>
public enum TabViewMode
{
    /// <summary>The graph with the sidebar and the commit detail pane.</summary>
    History,

    /// <summary>The working-tree view: Unstaged and Staged lists, the diff and the commit message box.</summary>
    WorkingTree,
}
