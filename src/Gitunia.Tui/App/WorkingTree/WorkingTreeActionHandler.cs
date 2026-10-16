using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Handles the actions of the working-tree view: opening and leaving it, moving between its lists and the cursor in them, and
/// copying a path. While the view is shown it owns every list and pane action, so none of them reaches the hidden graph or
/// sidebar. Everything it changes goes through commands, so the state decides and the views follow.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="pageHeight">Visible rows of the focused list, for the half-page motions.</param>
/// <param name="clipboard">Copies the path of a file.</param>
/// <param name="staging">Stages and unstages files.</param>
/// <param name="commits">The message box and the commit.</param>
public sealed class WorkingTreeActionHandler(
    WorkspaceHost host,
    Func<int> pageHeight,
    ClipboardCopier clipboard,
    StagingActions staging,
    CommitActions commits)
{
    private static readonly PaneKind[] Panes = [PaneKind.WorkingUnstaged, PaneKind.WorkingStaged, PaneKind.WorkingMessage];

    private static readonly HashSet<ActionId> LeftToOthers =
    [
        ActionIds.TabRename,
        ActionIds.InputAccept,
        ActionIds.Cancel,
        ActionIds.RepositoryRefresh,
        ActionIds.ToolCommitEditor,
        ActionIds.AppQuit,
    ];

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Returns true when the action was handled here and no other pane may act on it.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var isShown = tab.View.Mode == TabViewMode.WorkingTree;
        if (action == ActionIds.WorkingTreeToggle || (action == ActionIds.WorkingTreeOpen && !isShown))
        {
            Switch(tab, isShown && action == ActionIds.WorkingTreeToggle);
            return true;
        }

        if (isShown && commits.TryExecute(tab, action))
        {
            Message = commits.Notice;
            return true;
        }

        if (!isShown || IsLeftToOthers(action))
        {
            return false;
        }

        if (action == ActionIds.NavigateBack)
        {
            Send(tab, new LeaveWorkingTreeCommand());
        }
        else if (!TryFocus(tab, action) && !TryMove(tab, action, count) && !TryCopy(tab, action) && !TryStage(tab, action))
        {
            Message = $"{ActionCatalog.Describe(action)}: not available in the working-tree view";
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
        PaneKind target;
        if (action == ActionIds.PaneNext || action == ActionIds.PanePrevious)
        {
            target = PaneNavigator.Cycle(focused, action == ActionIds.PaneNext, Panes);
        }
        else if (action == ActionIds.PaneFocusUp || action == ActionIds.PaneFocusDown)
        {
            target = focused == PaneKind.WorkingMessage
                ? (action == ActionIds.PaneFocusUp ? ListPaneOf(tab) : focused)
                : PaneNavigator.StepVertically(focused, action == ActionIds.PaneFocusDown, Panes);
        }
        else if (action == ActionIds.PaneFocusLeft || action == ActionIds.PaneFocusRight)
        {
            // The lists are the left column; the message box (the diff will sit above it) is the right one.
            target = action == ActionIds.PaneFocusRight ? PaneKind.WorkingMessage : focused == PaneKind.WorkingMessage ? ListPaneOf(tab) : focused;
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

    private bool TryMove(RepositoryState tab, ActionId action, int? count)
    {
        if (tab.WorkingTree is not { } tree)
        {
            return ListMotion.TryMove(action, count, 0, 0, pageHeight(), out _);
        }

        var section = tab.View.FocusedPane == PaneKind.WorkingStaged ? WorkingTreeSection.Staged : WorkingTreeSection.Unstaged;
        var itemCount = section == WorkingTreeSection.Staged ? tree.Staged.Count : tree.Unstaged.Count;
        var current = tree.Selection is { } selection && selection.Section == section ? selection.Index : 0;
        if (!ListMotion.TryMove(action, count, current, itemCount, pageHeight(), out var target))
        {
            return false;
        }

        if (itemCount > 0)
        {
            Send(tab, new SelectWorkingTreeFileCommand(section, target));
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
            Message = "Select a file to copy its path";
            return true;
        }

        clipboard.Copy(WorkingTreeRowRenderer.Printable(selection.Path), "file path");
        return true;
    }

    private bool TryStage(RepositoryState tab, ActionId action)
    {
        if (!StagingActions.Handles(action))
        {
            return false;
        }

        Message = staging.Execute(tab, action);
        return true;
    }

    private static PaneKind ListPaneOf(RepositoryState tab) =>
        tab.WorkingTree?.Selection?.Section == WorkingTreeSection.Staged ? PaneKind.WorkingStaged : PaneKind.WorkingUnstaged;

    private static bool IsInFocusedList(RepositoryState tab, WorkingTreeSelection selection) =>
        (tab.View.FocusedPane == PaneKind.WorkingStaged) == (selection.Section == WorkingTreeSection.Staged);

    private void Send(RepositoryState tab, RepositoryCommand command) => host.Dispatch(new TabCommand(tab.Id, command));
}
