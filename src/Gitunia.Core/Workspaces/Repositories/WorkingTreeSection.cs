namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Which working-tree list a selection is in (docs/ux.md, workflow 2).</summary>
public enum WorkingTreeSection
{
    /// <summary>Worktree changes, untracked and conflicted paths.</summary>
    Unstaged,

    /// <summary>Changes recorded in the index.</summary>
    Staged,
}
