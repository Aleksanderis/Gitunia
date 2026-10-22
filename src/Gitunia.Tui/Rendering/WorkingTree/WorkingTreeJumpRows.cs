using Gitunia.Core.Jump;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>The files of one working-tree list as quick-jump rows: the path (a rename as old and new), after the status letter.</summary>
/// <param name="tree">The working tree, null while it is not loaded.</param>
/// <param name="section">The list.</param>
/// <param name="width">Columns of the list's text area.</param>
/// <param name="ellipsis">The ellipsis a cut path ends with.</param>
/// <param name="pathStyle">How the rows show their paths.</param>
public sealed class WorkingTreeJumpRows(
    WorkingTreeState? tree,
    WorkingTreeSection section,
    int width,
    string ellipsis,
    FilePathStyle pathStyle = FilePathStyle.FullPath) : IJumpRows
{
    public int Count => WorkingTreePaneRenderer.Count(tree, section);

    public int EllipsisWidth { get; } = TextWidth.Of(ellipsis);

    public string TextOf(int row) => WorkingTreeRowRenderer.PathLayoutText(
        EntryOf(row),
        section,
        pathStyle,
        ColumnsOf(row),
        ellipsis);

    /// <summary>The columns left for the path: the text area less the mark and a conflicted row's conflict
    /// text.</summary>
    public int ColumnsOf(int row) =>
        Math.Max(0, width - WorkingTreeRowRenderer.MarkColumns - WorkingTreeRowRenderer.ConflictColumns(EntryOf(row)));

    private StatusEntry EntryOf(int row) =>
        (section == WorkingTreeSection.Unstaged ? tree!.Unstaged : tree!.Staged)[row];
}
