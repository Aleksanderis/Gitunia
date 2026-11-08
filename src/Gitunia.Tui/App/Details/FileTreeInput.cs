using Gitunia.App;
using Gitunia.Core.Details;
using Gitunia.Core.Editing;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// The terminal side of the file trees of the detail pane: the Files tab (<c>Enter</c>/<c>l</c> open or close a
/// directory, <c>h</c> closes it or goes to the parent row, <c>/</c> filters the tree while typing, <c>Esc</c> clears
/// the filter) and the folders of the Changes tab's tree style (<c>o</c>, <c>l</c>, <c>h</c>). It reads what is under
/// the cursor from the rows the pane drew and turns the keys into <see cref="DetailCommand"/>s; the reducer decides.
/// It is also a prompt (the filter box), so it receives the typed keys while the box is open.
/// </summary>
/// <param name="host">The state and the commands.</param>
/// <param name="detailList">The list of the detail pane: it owns the cursor and shows the rows of the active tab.</param>
public sealed class FileTreeInput(Dispatcher host, ListView detailList)
    : IActionHandler, ITextKeyHandler, IPromptDisplay
{
    private const string FilterLabel = "/";
    private const string AppliedFilterLabel = "filter: ";

    public string StatusLine
    {
        get
        {
            if (host.Workspace.ActiveTab?.View is not { } view)
            {
                return string.Empty;
            }

            var tree = view.DetailTree;
            if ((!tree.IsEditingFilter && tree.Filter.Length == 0) || !DetailTreeRules.IsFilesTab(view))
            {
                return string.Empty;
            }

            return tree.IsEditingFilter ? $"{FilterLabel}{PromptCaret.Mark(tree.Filter, tree.Caret)}" : $"{AppliedFilterLabel}{tree.Filter}";
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var view = tab.View;
        if (DetailTreeRules.IsChangesTree(view) && TryExecuteInChanges(tab, action))
        {
            return true;
        }

        if (DetailTreeRules.IsEditingFilter(view))
        {
            return ExecuteWhileEditing(tab.Id, action);
        }

        if (action == ActionIds.DetailFilter)
        {
            host.Dispatch(new FileFilterStarted(tab.Id));

            return true;
        }

        if (!DetailTreeRules.IsFilesFocused(view))
        {
            return false;
        }

        return ExecuteInFiles(tab, action);
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (host.Workspace.ActiveTab is not { } tab || !DetailTreeRules.IsEditingFilter(tab.View))
        {
            return false;
        }

        host.Dispatch(new FileFilterKeyTyped(tab.Id, key));

        // Insert mode swallows every key, so a stray one never moves the list or fires a binding.
        return true;
    }

    private bool ExecuteInFiles(RepositoryState tab, ActionId action)
    {
        var cursor = FilesCursor();
        if (action == ActionIds.DetailOpenDiff)
        {
            host.Dispatch(new FileTreeOpened(tab.Id, cursor));
        }
        else if (action == ActionIds.DetailExpand)
        {
            host.Dispatch(new FileTreeExpanded(tab.Id, cursor));
        }
        else if (action == ActionIds.DetailCollapse)
        {
            host.Dispatch(new FileTreeCollapsed(tab.Id, cursor));
        }
        else if (action == ActionIds.NavigateBack && tab.View.DetailTree.Filter.Length > 0)
        {
            host.Dispatch(new FileFilterCleared(tab.Id));
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool ExecuteWhileEditing(TabId tabId, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            host.Dispatch(new FileFilterAccepted(tabId));
        }
        else if (action == ActionIds.Cancel)
        {
            host.Dispatch(new FileFilterCancelled(tabId));
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool TryExecuteInChanges(RepositoryState tab, ActionId action)
    {
        var cursor = ChangesCursorOf();
        var focused = DetailTreeRules.IsChangesTreeFocused(tab.View);
        if (action == ActionIds.DetailToggleFolder)
        {
            host.Dispatch(new ChangesFolderToggled(tab.Id, cursor));

            return true;
        }

        if (action == ActionIds.DetailExpand && focused)
        {
            host.Dispatch(new ChangesFolderExpanded(tab.Id, cursor));

            return true;
        }

        if (action != ActionIds.DetailCollapse || !focused)
        {
            return false;
        }

        var step = DetailTreeRules.ChangesCollapseStep(tab.View.DetailTree, cursor);
        host.Dispatch(new ChangesFolderCollapsed(tab.Id, cursor));

        return step != TreeStep.Leave;
    }

    private FileTreeCursor FilesCursor()
    {
        var rows = ContentOfList().Tree;
        var index = detailList.SelectedItem ?? -1;
        if (index < 0 || index >= rows.Count)
        {
            return new FileTreeCursor(null, -1);
        }

        return new FileTreeCursor(rows[index], FileTreeModel.ParentRow(rows, index));
    }

    private ChangesCursor ChangesCursorOf()
    {
        var content = ContentOfList();
        var row = detailList.SelectedItem ?? -1;

        return new ChangesCursor(content.FolderAt(row), ParentFolderRow(content, row));
    }

    private DetailContent ContentOfList() => (detailList.Source as DetailListSource)?.Content ?? DetailContent.Empty;

    // The row of the folder that holds the file or folder at row, -1 at the top level.
    private static int ParentFolderRow(DetailContent content, int row)
    {
        var fileIndex = content.FileIndexAt(row);
        var path = content.FolderAt(row) ?? (fileIndex >= 0 ? content.Files[fileIndex].Path : null);
        var slash = path?.LastIndexOf('/') ?? -1;
        if (slash < 0)
        {
            return -1;
        }

        var parent = path![..slash];
        for (var index = row - 1; index >= 0; index--)
        {
            if (content.FolderAt(index) == parent)
            {
                return index;
            }
        }

        return -1;
    }
}
