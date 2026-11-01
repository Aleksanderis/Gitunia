using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Worktrees;

/// <summary>Finds the worktree a selection stands for in the tab's worktree list.</summary>
internal static class WorktreeResolver
{
    /// <summary>The worktree of the selected sidebar row, null when the row is not a listed worktree.</summary>
    public static WorktreeInfo? OfRow(RepositoryState tab, WorktreeTarget target)
    {
        if (target is { RowKind: SidebarRowKind.Worktree, Path: { } path })
        {
            return Listed(tab, path);
        }

        return null;
    }

    /// <summary>The worktree of the selected worktree row, or the one that holds the selected local branch.</summary>
    public static WorktreeInfo? OfRowOrBranch(RepositoryState tab, WorktreeTarget target)
    {
        var path = target switch
        {
            { RowKind: SidebarRowKind.Worktree, Path: { } worktreePath } => worktreePath,
            { RowKind: SidebarRowKind.LocalBranch } => HolderOf(tab, target.Label),
            _ => null,
        };

        return path is null ? null : Listed(tab, path);
    }

    /// <summary>The path of the worktree that has the local branch checked out, null when none has.</summary>
    public static string? HolderOf(RepositoryState tab, string? name)
    {
        var reference = tab.Refs.FirstOrDefault(
            candidate => candidate.Kind == RefKind.LocalBranch && candidate.Name == name);

        return reference?.CheckedOutIn ?? (reference?.IsCurrent == true ? tab.CommandPath : null);
    }

    private static WorktreeInfo? Listed(RepositoryState tab, string path) =>
        tab.Worktrees.FirstOrDefault(worktree => RepositoryPathComparer.Instance.Equals(worktree.Path, path));
}
