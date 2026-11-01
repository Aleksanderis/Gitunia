namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>Which Changes view list a selection is in (docs/internal/ux-workflows.md, workflow 2).</summary>
public enum WorkingTreeSection
{
    /// <summary>Worktree changes, untracked and conflicted paths.</summary>
    Unstaged,

    /// <summary>Changes recorded in the index.</summary>
    Staged,
}
