using Gitunia.Core.FileTree;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>
/// The changed files of a commit laid out as a tree of folders for the Changes tab (see <see cref="PathTree"/>): folders
/// before files, each level in ordinal order. A folder in the collapsed set shows no rows below it.
/// </summary>
public static class ChangesTree
{
    /// <summary>The visible rows, top to bottom.</summary>
    /// <param name="files">The changed files of the commit.</param>
    /// <param name="collapsed">Paths of the folders that are closed, null when all are open.</param>
    public static IReadOnlyList<ChangesTreeRow> Rows(
        IReadOnlyList<CommitFileChange> files,
        IReadOnlySet<string>? collapsed = null) => PathTree
            .Rows([.. files.Select(file => file.Path)], collapsed)
            .Select(row => new ChangesTreeRow(row.Depth, row.Name, row.FolderPath, row.PathIndex))
            .ToList();
}
