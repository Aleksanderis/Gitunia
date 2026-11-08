using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>What the detail pane lists for the selected commit on its selected tab.</summary>
/// <param name="ShortHash">The short hash of the commit, empty when no commit is selected.</param>
/// <param name="Rows">All rows, top to bottom.</param>
/// <param name="FirstFileRow">Index of the first file row (in the tree style folder rows may come before it);
/// equals the row count when there are no files or the tab lists none.</param>
/// <param name="Files">The changed files (also on tabs that do not list them, for the title); on the Changes tab
/// a row shows <c>Files[n]</c> when <see cref="FileIndexAt"/> says so.</param>
/// <param name="Tab">The tab these rows belong to.</param>
/// <param name="FileCount">The number of changed files, null while the details are not loaded (the title shows <c>.. files</c> then instead of a count that would flicker from 0).</param>
public sealed record DetailContent(
    string ShortHash,
    IReadOnlyList<DetailRow> Rows,
    int FirstFileRow,
    IReadOnlyList<CommitFileChange> Files,
    BottomTab Tab,
    int? FileCount = null)
{
    /// <summary>How the file rows of the Changes tab show their paths.</summary>
    public FilePathStyle PathStyle { get; init; } = FilePathStyle.FullPath;

    /// <summary>On the Files tab the visible tree rows; row <c>n</c> of <see cref="Rows"/> shows <c>Tree[n]</c>. Empty
    /// while the tree is not shown (loading, failed, empty, other tabs).</summary>
    public IReadOnlyList<FileTreeRow> Tree { get; init; } = [];

    /// <summary>Index in <see cref="Files"/> of the file <paramref name="row"/> shows, -1 for any other
    /// row.</summary>
    public int FileIndexAt(int row) => row >= 0 && row < Rows.Count ? Rows[row].FileIndex : -1;

    /// <summary>
    /// The path of the file on <paramref name="row"/>: the Files tab lists a tree (a directory row is no file), the
    /// Changes tab the changed files. Null on any other row.
    /// </summary>
    /// <param name="row">The row.</param>
    public string? FilePathAt(int row)
    {
        if (Tree.Count > 0)
        {
            var isFile = row >= 0 && row < Tree.Count && Tree[row].Kind != FileTreeRowKind.Directory;

            return isFile ? Tree[row].Path : null;
        }

        var index = FileIndexAt(row);

        return index >= 0 && index < Files.Count ? Files[index].Path : null;
    }

    /// <summary>The path of the folder row <paramref name="row"/> (tree style), null for any other row.</summary>
    public string? FolderAt(int row) => row >= 0 && row < Rows.Count ? Rows[row].Folder : null;

    /// <summary>The row that shows <c>Files[fileIndex]</c>, -1 when no row does.</summary>
    public int RowOfFile(int fileIndex)
    {
        for (var row = 0; row < Rows.Count; row++)
        {
            if (Rows[row].FileIndex == fileIndex)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>No commit selected, on the Changes tab.</summary>
    public static DetailContent Empty { get; } = EmptyFor(BottomTab.Changes);

    /// <summary>No commit selected, on <paramref name="tab"/>.</summary>
    public static DetailContent EmptyFor(BottomTab tab) => new(string.Empty, [], 0, [], tab);
}
