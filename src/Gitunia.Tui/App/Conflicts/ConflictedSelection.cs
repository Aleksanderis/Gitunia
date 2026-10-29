using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.App.Conflicts;

/// <summary>Which file the conflict editor would open: the selected Unstaged row, when it is a conflicted
/// file.</summary>
public static class ConflictedSelection
{
    /// <summary>The conflicted entry under the Unstaged cursor, or null for a folder, another list or a plain
    /// file.</summary>
    /// <param name="tree">The working tree of the tab, null while the status is not loaded.</param>
    public static StatusEntry? Find(WorkingTreeState? tree)
    {
        if (tree is not { Selection: { Section: WorkingTreeSection.Unstaged, IsFolder: false } selection })
        {
            return null;
        }

        return tree.Unstaged.FirstOrDefault(entry =>
            entry.Path == selection.Path && entry.Kind == StatusEntryKind.Unmerged);
    }
}
