using Gitunia.Core.Jump;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>The files of one working-tree list as quick-jump rows: the path (a rename as old and new), after the status letter.</summary>
/// <param name="tree">The working tree, null while it is not loaded.</param>
/// <param name="section">The list.</param>
/// <param name="width">Columns of the list's text area.</param>
/// <param name="ellipsis">The ellipsis a cut path ends with.</param>
public sealed class WorkingTreeJumpRows(WorkingTreeState? tree, WorkingTreeSection section, int width, string ellipsis) : IJumpRows
{
    public int Count => WorkingTreePaneRenderer.Count(tree, section);

    public int EllipsisWidth { get; } = TextWidth.Of(ellipsis);

    public string TextOf(int row) =>
        WorkingTreeRowRenderer.PathText((section == WorkingTreeSection.Unstaged ? tree!.Unstaged : tree!.Staged)[row], section);

    public int ColumnsOf(int row) => Math.Max(0, width - WorkingTreeRowRenderer.MarkColumns);
}
