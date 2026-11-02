using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Handles the actions of the Changes view: opening and leaving it, moving between its lists and the diff and the cursor in
/// them, and copying a path. While the view is shown it owns every list and pane action, so none of them reaches the hidden graph
/// or sidebar; the actions of the diff go to the <see cref="WorkingDiffController"/> first. Everything it changes goes through
/// commands, so the state decides and the views follow.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="lists">The lists of the panes, whose visible rows size the half-page motions.</param>
/// <param name="clipboard">Copies the path of a file.</param>
/// <param name="commits">The message box and the commit.</param>
/// <param name="context">Staging, the diff box and Visual mode.</param>
public sealed partial class WorkingTreeActionHandler(
    Dispatcher host,
    IPaneLists lists,
    ClipboardCopier clipboard,
    CommitMessageInput commits,
    StagingContext context) : IActionHandler, IActionNotice
{
    private static readonly HashSet<ActionId> LeftToOthers =
    [
        ActionIds.TabRename,
        ActionIds.InputAccept,
        ActionIds.Cancel,
        ActionIds.RepositoryRefresh,
        ActionIds.ToolCommitEditor,
        ActionIds.FilesTogglePathStyle,
        ActionIds.FilesPathNameFirst,
        ActionIds.FilesPathFullPath,
        ActionIds.FilesPathTree,
        ActionIds.FileHistoryOpen,
    ];

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    string IActionNotice.Notice => Message;

    /// <summary>Returns true when the action was handled here and no other pane may act on it.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var isShown = tab.View.Mode == TabViewMode.WorkingTree;
        // Opening what is already open (a click on the dirty marker of the tab in its working tree, #331) is a silent no-op.
        if (action == ActionIds.WorkingTreeOpen && isShown)
        {
            return true;
        }

        if (action == ActionIds.WorkingTreeToggle || (action == ActionIds.WorkingTreeOpen && !isShown))
        {
            Switch(tab, isShown && action == ActionIds.WorkingTreeToggle);
            return true;
        }

        if (isShown && commits.TryExecute(tab, action))
        {
            return true;
        }

        if (!isShown || IsLeftToOthers(action))
        {
            return false;
        }

        if (TryVisual(tab, action, count))
        {
            Message = string.Empty;
        }
        else if (action == ActionIds.AppQuit)
        {
            // q quits from every pane (#333); an active range ended first, above.
            return false;
        }
        else if (TryFileJump(tab, action, count))
        {
            // ]f and [f picked another file of the list the diff follows (or said there is none).
        }
        else if (context.Diff?.TryExecute(action, count) == true)
        {
            Message = context.Diff.Notice;
        }
        else if (TryFolder(tab, action))
        {
            // za, h or l acted on the folder under the cursor (or had nothing to do).
        }
        else if (action == ActionIds.NavigateBack)
        {
            Send(tab, new LeaveWorkingTreeCommand());
        }
        else if (!TryFocus(tab, action) && !TryOpenDiff(tab, action) && !TryMove(tab, action, count) && !TryCopy(tab, action) && !TryStage(tab, action) && !TryDiscard(tab, action))
        {
            Message = $"{ActionCatalog.Describe(action)}: not available in the Changes view";
        }

        return true;
    }

    // Prompts (tab rename) and the cancel and accept of an open input still work in this view.
    private static bool IsLeftToOthers(ActionId action) => LeftToOthers.Contains(action);

    private void Switch(RepositoryState tab, bool leave)
    {
        if (leave)
        {
            Send(tab, new LeaveWorkingTreeCommand());
        }
        else if (tab.IsMissing)
        {
            Message = "The repository is missing: there is no working tree to show";
        }
        else
        {
            Send(tab, new OpenWorkingTreeCommand());
        }
    }

    private bool TryFocus(RepositoryState tab, ActionId action)
    {
        var focused = tab.View.FocusedPane;
        var selection = tab.WorkingTree?.Selection;
        PaneKind target;
        if (action == ActionIds.PaneNext || action == ActionIds.PanePrevious)
        {
            // The diff in the cycle is the one of the selected file's side; the message box comes last.
            List<PaneKind> cycle = [.. WorkingPanes.Lists];
            if (selection is not null)
            {
                cycle.Add(WorkingPanes.DiffOf(selection.Section));
            }

            cycle.Add(PaneKind.WorkingMessage);
            target = PaneNavigator.Cycle(focused, action == ActionIds.PaneNext, cycle);
        }
        else if (action == ActionIds.PaneFocusUp || action == ActionIds.PaneFocusDown)
        {
            target = StepVertically(focused, action == ActionIds.PaneFocusDown, selection);
        }
        else if (action == ActionIds.PaneFocusRight)
        {
            // The diff is the right column: from a list, the diff of the file under the cursor (the message box when there is none).
            target = focused;
            if (WorkingPanes.IsList(focused))
            {
                target = selection is null ? PaneKind.WorkingMessage : WorkingPanes.DiffOf(selection.Section);
            }
        }
        else if (action == ActionIds.PaneFocusLeft)
        {
            target = WorkingPanes.IsList(focused) ? focused : WorkingPanes.ListOf(selection?.Section ?? WorkingPanes.SectionOf(focused));
        }
        else if (action == ActionIds.WorkingTreeFocusUnstaged)
        {
            target = PaneKind.WorkingUnstaged;
        }
        else if (action == ActionIds.WorkingTreeFocusStaged)
        {
            target = PaneKind.WorkingStaged;
        }
        else if (action == ActionIds.WorkingTreeFocusMessage)
        {
            target = PaneKind.WorkingMessage;
        }
        else if (action == ActionIds.WorkingTreeFocusDiff)
        {
            // The diff of the file under the cursor, or the one already shown; without a file there is nothing to show.
            if (WorkingPanes.IsDiff(focused))
            {
                return true;
            }

            if (selection is null)
            {
                Message = "Select a file to see its diff";
                return true;
            }

            target = WorkingPanes.DiffOf(selection.Section);
        }
        else
        {
            return false;
        }

        if (target != focused)
        {
            Send(tab, new FocusPaneCommand(target));
        }

        return true;
    }

    // The lists are stacked on the left, the diff above the message box on the right.
    private static PaneKind StepVertically(PaneKind focused, bool down, WorkingTreeSelection? selection)
    {
        if (focused == PaneKind.WorkingMessage)
        {
            return !down && selection is not null ? WorkingPanes.DiffOf(selection.Section) : focused;
        }

        if (WorkingPanes.IsDiff(focused))
        {
            return down ? PaneKind.WorkingMessage : focused;
        }

        return PaneNavigator.StepVertically(focused, down, WorkingPanes.Lists);
    }

    // Enter on a file: its diff takes the focus (on a narrow screen it replaces the list until Esc).
    private bool TryOpenDiff(RepositoryState tab, ActionId action)
    {
        if (action != ActionIds.WorkingTreeOpenDiff)
        {
            return false;
        }

        if (tab.WorkingTree?.Selection is { } selection)
        {
            Send(tab, new FocusPaneCommand(WorkingPanes.DiffOf(selection.Section)));
        }
        else
        {
            Message = "Select a file to see its diff";
        }

        return true;
    }

    private bool TryMove(RepositoryState tab, ActionId action, int? count)
    {
        if (WorkingPanes.IsDiff(tab.View.FocusedPane))
        {
            // The diff's own motions went to the diff above; a list motion here has nothing to move.
            return false;
        }

        if (tab.WorkingTree is not { } tree)
        {
            return ListMotion.TryMove(action, count, 0, 0, PageHeight(tab), out _);
        }

        var section = tab.View.FocusedPane == PaneKind.WorkingStaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        var itemCount = tree.RowsOf(section).Count;
        var current = tree.Selection is { } selection && selection.Section == section ? selection.Index : 0;
        if (!ListMotion.TryMove(action, count, current, itemCount, PageHeight(tab), out var target))
        {
            return false;
        }

        if (itemCount > 0)
        {
            Send(tab, new SelectWorkingTreeFileCommand(section, target));
        }

        return true;
    }

    // za opens or closes the folder under the cursor. h closes an open folder, else moves to the parent folder;
    // l opens a closed folder. Neither changes the focus.
    private bool TryFolder(RepositoryState tab, ActionId action)
    {
        var isToggle = action == ActionIds.WorkingTreeToggleFolder;
        var isExpand = action == ActionIds.WorkingTreeExpand;
        if (!isToggle && !isExpand && action != ActionIds.WorkingTreeCollapse)
        {
            return false;
        }

        var section = WorkingTreeSection.Unstaged;
        if (tab.View.FocusedPane == PaneKind.WorkingStaged)
        {
            section = WorkingTreeSection.Staged;
        }

        if (tab.WorkingTree is { Selection: { } selection } tree
            && selection.Section == section
            && tree.PathStyle == FilePathStyle.Tree)
        {
            if (isToggle && selection.IsFolder)
            {
                Send(tab, new ToggleWorkingFolderCommand(section, selection.Path));
                return true;
            }

            if (isExpand)
            {
                if (selection.IsFolder && tree.CollapsedOf(section).Contains(selection.Path))
                {
                    Send(tab, new ToggleWorkingFolderCommand(section, selection.Path, false));
                }

                return true;
            }

            if (!isToggle && selection.IsFolder && !tree.CollapsedOf(section).Contains(selection.Path))
            {
                Send(tab, new ToggleWorkingFolderCommand(section, selection.Path, true));
                return true;
            }

            var parent = tree.RowsOf(section).ParentFolderRow(selection.Index);
            if (!isToggle && parent >= 0)
            {
                Send(tab, new SelectWorkingTreeFileCommand(section, parent));
                return true;
            }
        }

        // Nothing to fold or climb: h and l stay in the pane (Esc leaves the view).
        return true;
    }

    // ]f and [f in the diff pick the next or previous file of the list the shown file is in; the diff then follows it.
    private bool TryFileJump(RepositoryState tab, ActionId action, int? count)
    {
        if ((action != ActionIds.DiffNextFile && action != ActionIds.DiffPreviousFile)
            || !WorkingPanes.IsDiff(tab.View.FocusedPane)
            || tab.WorkingTree is not { Selection: { } selection } tree)
        {
            return false;
        }

        var forward = action == ActionIds.DiffNextFile;
        var rows = tree.RowsOf(selection.Section);
        var target = rows.StepFile(Math.Min(selection.Index, rows.Count - 1), forward, count ?? 1);
        if (rows.Count == 0 || target == selection.Index)
        {
            Message = forward ? "No next file" : "No previous file";
        }
        else
        {
            Send(tab, new SelectWorkingTreeFileCommand(selection.Section, target));
        }

        return true;
    }

    private bool TryCopy(RepositoryState tab, ActionId action)
    {
        if (action != ActionIds.WorkingTreeCopyPath)
        {
            return false;
        }

        if (tab.WorkingTree?.Selection is not { } selection || !IsInFocusedList(tab, selection))
        {
            Message = "Select a file or folder to copy its path";
            return true;
        }

        clipboard.Copy(
            WorkingTreeRowRenderer.Printable(selection.Path),
            selection.IsFolder ? "folder path" : "file path");
        return true;
    }

    // Range selection works in the diff (lines) and in the file lists (files): v and the mouse drag start it, y copies it, q ends it.
    private bool TryVisual(RepositoryState tab, ActionId action, int? count)
    {
        if (context.Visual is null || !(WorkingPanes.IsDiff(tab.View.FocusedPane) || WorkingPanes.IsList(tab.View.FocusedPane)))
        {
            return false;
        }

        var isRangeAction = action == ActionIds.VisualToggle || action == ActionIds.VisualCopy;
        return (isRangeAction || (context.Visual.IsActive && action == ActionIds.AppQuit)) && context.Visual.TryExecute(action, count);
    }

    private int PageHeight(RepositoryState tab) => lists.ListOf(tab.View.FocusedPane).Viewport.Height;

    private static bool IsInFocusedList(RepositoryState tab, WorkingTreeSelection selection) =>
        (tab.View.FocusedPane == PaneKind.WorkingStaged) == (selection.Section == WorkingTreeSection.Staged);

    private void Send(RepositoryState tab, RepositoryCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
