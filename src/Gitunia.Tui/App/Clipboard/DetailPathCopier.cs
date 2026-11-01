using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Copies the path of the file row selected in the detail pane; kept apart from the <see cref="NavigationActions"/> so it runs without a terminal.</summary>
/// <param name="clipboard">Copies the path.</param>
public sealed class DetailPathCopier(ClipboardCopier clipboard)
{
    /// <summary>Copies the path of the selected file row.</summary>
    /// <param name="source">The detail pane's content, null when there is no session.</param>
    /// <param name="selectedItem">The selected row of the detail list, null when none is.</param>
    /// <returns>A notice for the status bar when nothing was copied, empty when the path was copied.</returns>
    public string Copy(DetailListSource? source, int? selectedItem)
    {
        if (source is { Content.Tree.Count: > 0 }
            && selectedItem is { } row
            && row >= 0
            && row < source.Content.Tree.Count)
        {
            clipboard.Copy(source.Content.Tree[row].Path, "path");

            return string.Empty;
        }

        var cursor = selectedItem ?? -1;
        if (source?.Content.FolderAt(cursor) is { } folder)
        {
            clipboard.Copy(folder, "folder path");

            return string.Empty;
        }

        var index = source?.Content.FileIndexAt(cursor) ?? -1;
        if (source is null || index < 0 || index >= source.Content.Files.Count)
        {
            return "Select a file to copy its path";
        }

        clipboard.Copy(source.Content.Files[index].Path, "file path");
        return string.Empty;
    }
}
