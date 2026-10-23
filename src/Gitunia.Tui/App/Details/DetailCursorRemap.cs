using Gitunia.Core.Diff;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// Toggling the path style relists the same files in other rows (the tree style adds folder rows): finds the row the
/// cursor goes to so it stays on the file or folder it was on.
/// </summary>
public static class DetailCursorRemap
{
    /// <summary>The row to move the cursor to, -1 when it stays where it is.</summary>
    /// <param name="previous">The rows before the style changed.</param>
    /// <param name="content">The rows now.</param>
    /// <param name="cursor">The cursor row in <paramref name="previous"/>.</param>
    public static int Row(DetailContent previous, DetailContent content, int? cursor)
    {
        if (previous.PathStyle == content.PathStyle
            || previous.ShortHash != content.ShortHash
            || previous.Tab != BottomTab.Changes
            || content.Tab != BottomTab.Changes
            || cursor is not { } row)
        {
            return -1;
        }

        var file = previous.FileIndexAt(row);
        if (file >= 0)
        {
            var fileRow = content.RowOfFile(file);

            // A file hidden by a closed folder: the cursor goes to the deepest folder row above it (open ancestors come first, the closed one is last).
            return fileRow >= 0 || file >= previous.Files.Count
                ? fileRow
                : content.Rows.ToList().FindLastIndex(candidate =>
                    candidate.Folder is { } closed && FileTreePath.IsUnder(closed, previous.Files[file].Path));
        }

        if (previous.FolderAt(row) is not { } folder)
        {
            return -1;
        }

        var folderRow = content.Rows.ToList().FindIndex(candidate => candidate.Folder == folder);
        if (folderRow >= 0)
        {
            return folderRow;
        }

        // A flat list has no folder rows: the cursor goes to the first file below the folder.
        var first = content.Files.ToList().FindIndex(candidate => FileTreePath.IsUnder(folder, candidate.Path));

        return first < 0 ? -1 : content.RowOfFile(first);
    }
}
