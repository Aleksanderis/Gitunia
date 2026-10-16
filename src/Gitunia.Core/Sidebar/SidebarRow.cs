using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Core.Sidebar;

/// <summary>One row of the sidebar list, ready to draw. Plain data so the renderer and the tests need no repository.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Section">The section the row belongs to (for a section header, the section itself).</param>
/// <param name="Label">Branch, tag or stash name, worktree directory name, or the section title.</param>
/// <param name="Annotation">
/// Secondary text: for a branch, the directory name of the worktree that has it checked out; for a worktree, its branch
/// (or <c>detached</c>) and lock or prune state; for a stash, its message. Empty when there is none.
/// </param>
/// <param name="CommitHash">The commit the row selects in the graph, null for a section or when there is none (unborn).</param>
/// <param name="IsCurrent">The branch HEAD is on, or the worktree this tab shows.</param>
/// <param name="IsExpanded">For a section header, whether its entries are listed.</param>
/// <param name="Count">For a section header, the number of entries (matching the filter).</param>
/// <param name="Ahead">Commits ahead of the upstream, null when unknown or without upstream.</param>
/// <param name="Behind">Commits behind the upstream, null when unknown or without upstream.</param>
/// <param name="Path">For a worktree, its absolute path; null for every other row.</param>
/// <param name="Changes">For a worktree, the status read there (changes, ahead, behind); null until read.</param>
public sealed record SidebarRow(
    SidebarRowKind Kind,
    SidebarSection Section,
    string Label,
    string Annotation,
    string? CommitHash,
    bool IsCurrent,
    bool IsExpanded,
    int Count,
    int? Ahead,
    int? Behind,
    string? Path = null,
    WorktreeStatus? Changes = null)
{
    /// <summary>The row is a section header.</summary>
    public bool IsSection => Kind == SidebarRowKind.Section;
}
