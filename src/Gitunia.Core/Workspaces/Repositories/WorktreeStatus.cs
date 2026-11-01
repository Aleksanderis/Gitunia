using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>What <c>git status</c> reports for one worktree, as the sidebar's worktree overview shows it.</summary>
/// <param name="Staged">Paths with a change in the index.</param>
/// <param name="Changed">Paths with an unstaged change, plus untracked paths.</param>
/// <param name="Conflicted">Paths with an unresolved conflict.</param>
/// <param name="Ahead">Commits ahead of the upstream, 0 without an upstream.</param>
/// <param name="Behind">Commits behind the upstream, 0 without an upstream.</param>
public sealed record WorktreeStatus(int Staged, int Changed, int Conflicted, int Ahead, int Behind)
{
    /// <summary>There is anything for <c>git status</c> to list.</summary>
    public bool IsDirty => Staged > 0 || Changed > 0 || Conflicted > 0;

    /// <summary>Counts a full status read: the entries the Changes view lists, ignored paths excluded.</summary>
    public static WorktreeStatus From(StatusSnapshot status)
    {
        var staged = 0;
        var changed = 0;
        var conflicted = 0;
        foreach (var entry in status.Entries)
        {
            if (entry.Kind == StatusEntryKind.Unmerged)
            {
                conflicted++;
                continue;
            }

            if (entry.IsStaged)
            {
                staged++;
            }

            if (entry.IsUnstaged || entry.Kind == StatusEntryKind.Untracked)
            {
                changed++;
            }
        }

        return new WorktreeStatus(staged, changed, conflicted, status.Branch.Ahead ?? 0, status.Branch.Behind ?? 0);
    }
}
