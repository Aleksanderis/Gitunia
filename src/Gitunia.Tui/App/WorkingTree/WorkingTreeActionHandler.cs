using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Discard;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
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
/// <param name="staging">Stages and unstages files, hunks and lines.</param>
/// <param name="commits">The message box and the commit.</param>
public sealed class WorkingTreeActionHandler(
    Dispatcher host,
    IPaneLists lists,
    ClipboardCopier clipboard,
    StagingActions staging,
    CommitMessageInput commits) : IActionHandler, IActionNotice
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
    ];

    /// <summary>The diff box, which acts on its own actions while it has focus; null in tests that do not need it.</summary>
    public WorkingDiffController? Diff { get; set; }

    /// <summary>Visual mode, which the diff box uses for line ranges; null in tests that do not need it.</summary>
    public VisualRangeInput? Visual { get; set; }

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
        else if (Diff?.TryExecute(action, count) == true)
        {
            Message = Diff.Notice;
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
        if (Visual is null || !(WorkingPanes.IsDiff(tab.View.FocusedPane) || WorkingPanes.IsList(tab.View.FocusedPane)))
        {
            return false;
        }

        var isRangeAction = action == ActionIds.VisualToggle || action == ActionIds.VisualCopy;
        return (isRangeAction || (Visual.IsActive && action == ActionIds.AppQuit)) && Visual.TryExecute(action, count);
    }

    private bool TryStage(RepositoryState tab, ActionId action)
    {
        if (!StagingActions.Handles(action))
        {
            return false;
        }

        if (WorkingPanes.IsDiff(tab.View.FocusedPane))
        {
            Message = StageFromDiff(tab, action);
            return true;
        }

        // A Visual range of files: stage and unstage act on all of them and the range ends once the operation started.
        var range = ListRange(tab);
        var isRangeAction = action == ActionIds.WorkingTreeStage || action == ActionIds.WorkingTreeUnstage;
        Message = staging.Execute(tab, action, isRangeAction ? range : null);

        // The range ends once the operation started; a refusal (posted by the reducer) keeps it.
        if (Message.Length == 0 && isRangeAction && staging.IsRunning(tab.Id))
        {
            Visual?.End();
        }

        return true;
    }

    // The Visual range of the focused file list, null when there is none.
    private RowRange? ListRange(RepositoryState tab) =>
        Visual is { IsActive: true, Range: { } range } && WorkingPanes.IsList(tab.View.FocusedPane) ? range : null;

    // In the diff the same keys act on the hunk under the cursor, or on the Visual lines.
    private string StageFromDiff(RepositoryState tab, ActionId action)
    {
        var isStage = action == ActionIds.WorkingTreeStage;
        var isUnstage = action == ActionIds.WorkingTreeUnstage;
        if (!isStage && !isUnstage)
        {
            return "Focus a file list first";
        }

        var isUnstagedSide = tab.View.FocusedPane == PaneKind.WorkingUnstagedDiff;
        if (isStage != isUnstagedSide)
        {
            return isStage ? "Stage works in the Unstaged diff" : "Unstage works in the Staged diff";
        }

        if (Diff is null)
        {
            return "The diff is not available";
        }

        var capture = Diff.CaptureStaging(Visual?.Range);
        if (capture.Request is not { } request)
        {
            return capture.Notice;
        }

        var notice = staging.ExecuteSelection(tab, request, new DiffCursorMove(capture.HunkIndex, capture.ChangesBefore));
        if (notice.Length == 0 && staging.IsRunning(tab.Id))
        {
            Visual?.End();
        }

        return notice;
    }

    // In the list the key acts on the file or folder under the cursor, or on the files of the Visual range; in the diff on
    // the hunk under the cursor or the Visual lines. The reducer asks first and the range ends once the user confirmed.
    private bool TryDiscard(RepositoryState tab, ActionId action)
    {
        if (action != ActionIds.WorkingTreeDiscard)
        {
            return false;
        }

        if (WorkingPanes.IsDiff(tab.View.FocusedPane))
        {
            Message = DiscardFromDiff(tab);
        }
        else
        {
            host.Dispatch(new DiscardRequested(tab.Id, ListRange(tab)));
        }

        return true;
    }

    private string DiscardFromDiff(RepositoryState tab)
    {
        if (tab.View.FocusedPane != PaneKind.WorkingUnstagedDiff)
        {
            return "Discard works in the Unstaged diff: unstage the changes first";
        }

        if (Diff is not { } diff)
        {
            return "The diff is not available";
        }

        var capture = diff.CaptureStaging(Visual?.Range);
        if (capture.Request is not { } request)
        {
            return capture.Notice;
        }

        var move = new DiffCursorMove(capture.HunkIndex, capture.ChangesBefore);
        host.Dispatch(new DiscardSelectionRequested(tab.Id, request.ToSelection(), move));

        return string.Empty;
    }

    private int PageHeight(RepositoryState tab) => lists.ListOf(tab.View.FocusedPane).Viewport.Height;

    private static bool IsInFocusedList(RepositoryState tab, WorkingTreeSelection selection) =>
        (tab.View.FocusedPane == PaneKind.WorkingStaged) == (selection.Section == WorkingTreeSection.Staged);

    private void Send(RepositoryState tab, RepositoryCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
