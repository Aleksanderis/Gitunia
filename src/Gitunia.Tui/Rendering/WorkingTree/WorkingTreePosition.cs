using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>The status bar's position of the working-tree view: the one-based row of the cursor and the number of files in its list.</summary>
/// <param name="Selected">One-based row of the cursor, 0 when nothing is selected.</param>
/// <param name="Count">Files in the cursor's list.</param>
public readonly record struct WorkingTreePosition(int Selected, int Count)
{
    /// <summary>The position of the selection in <paramref name="tree"/>; 0 of 0 while the status is not loaded or no file is selected.</summary>
    public static WorkingTreePosition From(WorkingTreeState? tree)
    {
        if (tree?.Selection is not { } selection)
        {
            return new WorkingTreePosition(0, 0);
        }

        var count = WorkingTreePaneRenderer.Count(tree, selection.Section);
        return new WorkingTreePosition(Math.Min(selection.Index, Math.Max(0, count - 1)) + 1, count);
    }
}
