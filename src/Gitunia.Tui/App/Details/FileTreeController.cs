using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// The keys of the Files tab of the detail pane: <c>Enter</c>/<c>l</c> open or close a directory (on a file: show its
/// content in the diff pane's place), <c>h</c> closes it
/// or goes to the parent row (and leaves the pane at the top), <c>/</c> filters the tree by path while typing,
/// <c>Esc</c> clears the filter. It is also a prompt (the filter box), so it owns Insert mode while the filter is
/// edited. Runs without a terminal.
/// </summary>
/// <param name="host">The workspace: which pane is focused and which bottom tab is selected.</param>
/// <param name="activeSession">The active tab's session, null when there is none.</param>
/// <param name="selectedRow">The cursor row of the detail list.</param>
/// <param name="moveCursor">Moves the cursor of the detail list to a row.</param>
/// <param name="setMode">Switches the key mode (Insert while the filter is typed).</param>
/// <param name="refresh">Rebuilds the detail pane after the tree changed.</param>
/// <param name="openFile">Shows the content of a file row in the diff pane; returns a notice when it cannot.</param>
public sealed class FileTreeController(
    WorkspaceHost host,
    Func<TabSession?> activeSession,
    Func<int?> selectedRow,
    Action<int> moveCursor,
    Action<KeyMode> setMode,
    Action refresh,
    Func<FileTreeRow, string> openFile) : IPromptHandler
{
    private const string FilterLabel = "/";
    private const string AppliedFilterLabel = "filter: ";
    private const string WrongTabNotice = "The filter works on the Files tab of the detail pane";

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (activeSession()?.Tree is not { } tree
                || (!tree.IsEditingFilter && tree.Filter.Length == 0)
                || !IsFilesTab())
            {
                return string.Empty;
            }

            return tree.IsEditingFilter ? $"{FilterLabel}{tree.Filter}" : $"{AppliedFilterLabel}{tree.Filter}";
        }
    }

    /// <summary>
    /// Maps the keys the Files tab shares with other tabs back to the action they mean there: <c>h</c> is "back to
    /// the graph" everywhere, except where it closes a directory or climbs to the parent.
    /// </summary>
    public ActionId Resolve(ActionId action)
    {
        if (action != ActionIds.DetailCollapse)
        {
            return action;
        }

        return IsFilesFocused() && CollapseStep() != CollapseKind.Leave ? action : ActionIds.NavigateBack;
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (activeSession()?.Tree is not { } tree)
        {
            return false;
        }

        if (tree.IsEditingFilter && IsFilesTab())
        {
            return ExecuteWhileEditing(tree, action);
        }

        if (action == ActionIds.DetailFilter)
        {
            StartFilter(tree);

            return true;
        }

        if (!IsFilesFocused())
        {
            return false;
        }

        if (action == ActionIds.DetailOpenDiff)
        {
            Open(tree);
        }
        else if (action == ActionIds.DetailCollapse)
        {
            Collapse(tree);
        }
        else if (action == ActionIds.NavigateBack && tree.Filter.Length > 0)
        {
            ChangeFilter(tree, string.Empty);
        }
        else
        {
            return false;
        }

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (activeSession()?.Tree is not { IsEditingFilter: true } tree || !IsFilesTab())
        {
            return false;
        }

        if (PromptTextEditor.Apply(tree.Filter, key) is { } text)
        {
            ChangeFilter(tree, text);
        }

        // Insert mode swallows every key, so a stray one never moves the list or fires a binding.
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private bool ExecuteWhileEditing(FileTreeSession tree, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            tree.IsEditingFilter = false;
            setMode(KeyMode.Normal);
            refresh();
        }
        else if (action == ActionIds.Cancel)
        {
            tree.IsEditingFilter = false;
            setMode(KeyMode.Normal);
            ChangeFilter(tree, string.Empty);
        }
        else
        {
            return false;
        }

        return true;
    }

    private void StartFilter(FileTreeSession tree)
    {
        if (!IsFilesTab())
        {
            Notice = WrongTabNotice;

            return;
        }

        tree.IsEditingFilter = true;
        setMode(KeyMode.Insert);
        refresh();
    }

    private void ChangeFilter(FileTreeSession tree, string text)
    {
        tree.SetFilter(text);
        refresh();
        moveCursor(0);
    }

    private void Open(FileTreeSession tree)
    {
        if (CurrentRow() is not { } row)
        {
            return;
        }

        if (row.Kind != FileTreeRowKind.Directory)
        {
            Notice = openFile(row);

            return;
        }

        if (!(row.IsExpanded ? tree.Collapse(row) : tree.Expand(row)))
        {
            return;
        }

        refresh();
    }

    private void Collapse(FileTreeSession tree)
    {
        var step = CollapseStep();
        if (step == CollapseKind.Close && CurrentRow() is { } row)
        {
            tree.Collapse(row);
            refresh();
        }
        else if (step == CollapseKind.Parent && selectedRow() is { } index)
        {
            moveCursor(FileTreeModel.ParentRow(ContentRows(), index));
        }
    }

    // Close an open directory, otherwise climb to the parent row, and at the top leave the pane.
    private CollapseKind CollapseStep()
    {
        var rows = ContentRows();
        if (selectedRow() is not { } index || index < 0 || index >= rows.Count)
        {
            return CollapseKind.Leave;
        }

        var filtered = activeSession()?.Tree?.Filter.Length > 0;
        if (rows[index] is { Kind: FileTreeRowKind.Directory, IsExpanded: true } && !filtered)
        {
            return CollapseKind.Close;
        }

        return FileTreeModel.ParentRow(rows, index) >= 0 ? CollapseKind.Parent : CollapseKind.Leave;
    }

    private IReadOnlyList<FileTreeRow> ContentRows() => activeSession()?.DetailSource.Content.Tree ?? [];

    private FileTreeRow? CurrentRow()
    {
        var rows = ContentRows();

        return selectedRow() is { } index && index >= 0 && index < rows.Count ? rows[index] : null;
    }

    private bool IsFilesTab() => host.Workspace.ActiveTab?.View.BottomTab == BottomTab.Files;

    private bool IsFilesFocused() =>
        IsFilesTab() && host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Detail;

    private enum CollapseKind
    {
        Close,
        Parent,
        Leave,
    }
}
