using Gitunia.Core.Sidebar;

namespace Gitunia.Core.Worktrees;

/// <summary>What a worktree action acts on, as the user saw it when pressing the key: the sidebar row under the cursor
/// and the commit selected in the graph.</summary>
/// <param name="RowKind">The kind of the sidebar row, null when there is none.</param>
/// <param name="Label">The name shown on the row (the branch name for a local branch).</param>
/// <param name="Path">The absolute path of a worktree row, null for every other row.</param>
/// <param name="RowCommitHash">The commit the row selects, null when it has none.</param>
/// <param name="GraphCommit">The commit selected in the graph (the start point of a new branch), null for none.</param>
public sealed record WorktreeTarget(
    SidebarRowKind? RowKind,
    string? Label,
    string? Path,
    string? RowCommitHash,
    string? GraphCommit);
