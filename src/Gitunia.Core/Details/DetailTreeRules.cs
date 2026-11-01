using Gitunia.Core.FileTree;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Details;

/// <summary>
/// The facts the file tree keys depend on, shared by the reducer and the terminal layer (which asks before it
/// dispatches whether a key is the tree's, so the keys that are not fall through to the next handler).
/// </summary>
public static class DetailTreeRules
{
    /// <summary>The Files tab is showing.</summary>
    public static bool IsFilesTab(RepositoryViewState view) => view.BottomTab == BottomTab.Files;

    /// <summary>The Files tab is showing and the detail pane has focus.</summary>
    public static bool IsFilesFocused(RepositoryViewState view) =>
        IsFilesTab(view) && view.FocusedPane == PaneKind.Detail;

    /// <summary>The Changes tab shows its tree style.</summary>
    public static bool IsChangesTree(RepositoryViewState view) =>
        view is { BottomTab: BottomTab.Changes, PathStyle: FilePathStyle.Tree };

    /// <summary>The Changes tab shows its tree style and the detail pane has focus.</summary>
    public static bool IsChangesTreeFocused(RepositoryViewState view) =>
        IsChangesTree(view) && view.FocusedPane == PaneKind.Detail;

    /// <summary>The filter box of the Files tab is open and gets the typed keys.</summary>
    public static bool IsEditingFilter(RepositoryViewState view) =>
        IsFilesTab(view) && view.DetailTree.IsEditingFilter;

    /// <summary>What h does on a row of the Changes tab: close an open folder, otherwise climb to the parent.</summary>
    public static TreeStep ChangesCollapseStep(DetailTreeState tree, ChangesCursor cursor)
    {
        if (cursor.Folder is { } folder && !tree.ClosedFolders.Contains(folder))
        {
            return TreeStep.Close;
        }

        return cursor.ParentRow >= 0 ? TreeStep.Parent : TreeStep.Leave;
    }

    /// <summary>What h does on a row of the Files tab: close an open directory, otherwise climb to the parent.</summary>
    public static TreeStep FilesCollapseStep(DetailTreeState tree, FileTreeCursor cursor)
    {
        if (cursor.Row is not { } row)
        {
            return TreeStep.Leave;
        }

        if (row is { Kind: FileTreeRowKind.Directory, IsExpanded: true } && tree.Filter.Length == 0)
        {
            return TreeStep.Close;
        }

        return cursor.ParentRow >= 0 ? TreeStep.Parent : TreeStep.Leave;
    }
}
