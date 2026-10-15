using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Workspaces.Repositories;

/// <summary>The full status of one tab and the file selected in it. Pure data: refreshed by <see cref="WorkingTreeLoadedCommand"/>.</summary>
/// <param name="Snapshot">The last status read.</param>
/// <param name="Selection">The selected file, null when the lists are empty.</param>
public sealed record WorkingTreeState(StatusSnapshot Snapshot, WorkingTreeSelection? Selection)
{
    /// <summary>Entries of the Unstaged list, in git's order: worktree changes, untracked and conflicted paths.</summary>
    public IReadOnlyList<StatusEntry> Unstaged => Snapshot.Entries.Where(IsUnstagedRow).ToList();

    /// <summary>Entries of the Staged list, in git's order.</summary>
    public IReadOnlyList<StatusEntry> Staged => Snapshot.Entries.Where(entry => entry.IsStaged).ToList();

    /// <summary>Builds the state for a new snapshot, keeping the selection when its file is still listed.</summary>
    /// <remarks>
    /// Order of preference: the same path in the same list; the same path in the other list (it was staged or unstaged);
    /// the row now at the old index (clamped); the first row of the other list. Without a previous selection the first row is chosen.
    /// Never selects when both lists are empty.
    /// </remarks>
    public static WorkingTreeState Reconcile(WorkingTreeState? previous, StatusSnapshot snapshot)
    {
        var next = new WorkingTreeState(snapshot, null);
        var unstaged = next.Unstaged;
        var staged = next.Staged;
        var old = previous?.Selection;
        WorkingTreeSelection? selection = null;
        if (old is not null)
        {
            var oldIsUnstaged = old.Section == WorkingTreeSection.Unstaged;
            var same = oldIsUnstaged ? unstaged : staged;
            var other = oldIsUnstaged ? staged : unstaged;
            var otherSection = oldIsUnstaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
            var at = IndexOf(same, old.Path);
            if (at >= 0)
            {
                selection = new WorkingTreeSelection(old.Section, old.Path, at);
            }
            else if ((at = IndexOf(other, old.Path)) >= 0)
            {
                selection = new WorkingTreeSelection(otherSection, old.Path, at);
            }
            else if (same.Count > 0)
            {
                var index = Math.Min(old.Index, same.Count - 1);
                selection = new WorkingTreeSelection(old.Section, same[index].Path, index);
            }
            else if (other.Count > 0)
            {
                selection = new WorkingTreeSelection(otherSection, other[0].Path, 0);
            }
        }
        else if (unstaged.Count > 0)
        {
            selection = new WorkingTreeSelection(WorkingTreeSection.Unstaged, unstaged[0].Path, 0);
        }
        else if (staged.Count > 0)
        {
            selection = new WorkingTreeSelection(WorkingTreeSection.Staged, staged[0].Path, 0);
        }

        return next with { Selection = selection };
    }

    /// <summary>Whether two snapshots hold the same content (the record's list members compare by reference).</summary>
    public static bool SameContent(StatusSnapshot first, StatusSnapshot second) =>
        first.Branch == second.Branch
        && first.Entries.SequenceEqual(second.Entries)
        && first.Warnings.SequenceEqual(second.Warnings);

    private static bool IsUnstagedRow(StatusEntry entry) => entry.Kind switch
    {
        StatusEntryKind.Ignored => false,
        StatusEntryKind.Untracked or StatusEntryKind.Unmerged => true,
        _ => entry.IsUnstaged,
    };

    private static int IndexOf(IReadOnlyList<StatusEntry> list, string path)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Path == path)
            {
                return i;
            }
        }

        return -1;
    }
}
