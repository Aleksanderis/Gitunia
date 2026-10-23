using Gitunia.Core.Jump;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.Rendering.Files;
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

    public string TextOf(int row)
    {
        if (tree!.RowsOf(section) is { IsTree: true } rows)
        {
            var found = rows[row];

            return found.IsFolder
                ? WorkingTreeRowRenderer.FolderText(found)
                : WorkingTreeRowRenderer.TreeName(EntryOf(row), found, section);
        }

        return WorkingTreeRowRenderer.PathLayoutText(EntryOf(row), section, pathStyle, ColumnsOf(row), ellipsis);
    }

    /// <summary>The columns left for the path: the text area less the indent, the mark and a conflicted row's conflict
    /// text.</summary>
    public int ColumnsOf(int row)
    {
        if (tree!.RowsOf(section) is { IsTree: true } rows)
        {
            var found = rows[row];
            var indent = TextWidth.Of(TreeRowText.Indent(found.Depth));

            return found.IsFolder
                ? Math.Max(0, width - indent)
                : Math.Max(
                    0,
                        width - indent - WorkingTreeRowRenderer.MarkColumns
                        - WorkingTreeRowRenderer.ConflictColumns(EntryOf(row)));
        }

        return Math.Max(
            0,
            width - WorkingTreeRowRenderer.MarkColumns - WorkingTreeRowRenderer.ConflictColumns(EntryOf(row)));
    }

    // The file of a row; a folder row has none.
    private StatusEntry EntryOf(int row) => tree!.EntriesOf(section)[tree.RowsOf(section)[row].EntryIndex];
}
