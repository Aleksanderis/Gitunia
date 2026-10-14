using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Clipboard;

/// <summary>Copies the path of the file row selected in the detail pane; kept apart from the <see cref="ActionRouter"/> so it runs without a terminal.</summary>
/// <param name="clipboard">Copies the path.</param>
public sealed class DetailPathCopier(ClipboardCopier clipboard)
{
    /// <summary>Copies the path of the selected file row.</summary>
    /// <param name="source">The detail pane's content, null when there is no session.</param>
    /// <param name="selectedItem">The selected row of the detail list, null when none is.</param>
    /// <returns>A notice for the status bar when nothing was copied, empty when the path was copied.</returns>
    public string Copy(DetailListSource? source, int? selectedItem)
    {
        var index = (selectedItem ?? -1) - (source?.Content.FirstFileRow ?? 0);
        if (source is null || index < 0 || index >= source.Content.Files.Count)
        {
            return "Select a file to copy its path";
        }

        clipboard.Copy(source.Content.Files[index].Path, "file path");
        return string.Empty;
    }
}
