using Gitunia.Core.Diff;

namespace Gitunia.Core.DiffPane;

/// <summary>
/// Which part of the loaded diff the pane shows: one file, or the files of a folder. The pane holds one file only, the
/// one under the file list's cursor, so the loaded diff is cut to it. Pure functions of the pane state and the diff.
/// </summary>
public static class DiffShown
{
    /// <summary>
    /// Takes the pending file or folder from a loaded diff (nothing is taken while it loads, so the wish survives a
    /// reload) and remembers which file the pane shows.
    /// </summary>
    /// <param name="state">The pane state.</param>
    /// <param name="full">The diff the store holds.</param>
    /// <param name="isLoaded">The diff has loaded (not loading or failed).</param>
    public static DiffPaneState Refresh(DiffPaneState state, DiffDocument full, bool isLoaded)
    {
        var path = isLoaded ? state.PendingFile : null;
        var next = state;
        if (path is not null)
        {
            next = next with { PendingFile = null, ShownFolder = null };
        }

        if (isLoaded && next.PendingFolder is { } pendingFolder)
        {
            next = next with { PendingFolder = null, ShownFolder = pendingFolder };
        }

        var folderFiles = isLoaded && next.ShownFolder is { } folder ? full.FileIndexesUnder(folder) : [];
        if (isLoaded && folderFiles.Count == 0)
        {
            // The folder has no change in this commit: the pane falls back to a single file.
            next = next with { ShownFolder = null };
        }

        var file = FileToShow(full, folderFiles, path ?? next.ShownPath);

        return next with { ShownPath = file >= 0 ? full.FilePaths[file][0] : null };
    }

    /// <summary>The files of the loaded diff the pane shows after <see cref="Refresh"/>.</summary>
    /// <param name="state">The pane state.</param>
    /// <param name="full">The diff the store holds.</param>
    /// <param name="isLoaded">The diff has loaded (not loading or failed).</param>
    public static DiffShownCut Cut(DiffPaneState state, DiffDocument full, bool isLoaded)
    {
        var folderFiles = isLoaded && state.ShownFolder is { } folder ? full.FileIndexesUnder(folder) : [];
        var file = FileToShow(full, folderFiles, state.ShownPath);
        var built = folderFiles.Count > 0 ? state.ShownFolder : null;

        return new DiffShownCut(file, folderFiles, built);
    }

    /// <summary>The document the pane shows: the files of the folder, the one file, or the whole diff when it has none.</summary>
    /// <param name="full">The diff the store holds.</param>
    /// <param name="cut">Which part to show.</param>
    public static DiffDocument DocumentOf(DiffDocument full, DiffShownCut cut)
    {
        if (cut.FolderFiles.Count > 0)
        {
            return full.ForFiles(cut.FolderFiles);
        }

        return cut.File >= 0 ? full.ForFile(cut.File) : full;
    }

    // The index of the file the pane shows: the first of a folder's files, else the wanted one (-1 when none).
    private static int FileToShow(DiffDocument full, IReadOnlyList<int> folderFiles, string? wanted)
    {
        if (folderFiles.Count > 0)
        {
            return folderFiles[0];
        }

        if (full.FilePaths.Count == 0)
        {
            return -1;
        }

        return Math.Max(0, wanted is null ? 0 : full.FileIndexOf(wanted));
    }
}
