using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>Which file the conflict editor would open: the selected Unstaged row when it is a conflicted file, else the
/// first conflicted file of the working tree (one the editor takes, else any).</summary>
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

    /// <summary>The first conflicted file of the Unstaged list (the order it is listed in), from any pane.</summary>
    /// <param name="tree">The working tree of the tab, null while the status is not loaded.</param>
    public static StatusEntry? FindFirst(WorkingTreeState? tree)
    {
        var conflicted = tree?.Unstaged.Where(entry => entry.Kind == StatusEntryKind.Unmerged).ToList();

        return conflicted?.FirstOrDefault(entry => ConflictEditorSupport.CanOpen(entry.Conflict))
            ?? conflicted?.FirstOrDefault();
    }

    /// <summary>The file the editor key opens in the tab: the one under the Unstaged cursor, else the first conflicted
    /// one.</summary>
    /// <param name="tab">The tab.</param>
    public static StatusEntry? ToOpen(RepositoryState tab) => UnderCursor(tab) ?? FindFirst(tab.WorkingTree);

    /// <summary>The conflicted file under the cursor when the Unstaged list or its diff has the focus, else
    /// null.</summary>
    /// <param name="tab">The tab.</param>
    public static StatusEntry? UnderCursor(RepositoryState tab)
    {
        if (tab.View.FocusedPane is PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff)
        {
            return Find(tab.WorkingTree);
        }

        return null;
    }
}
