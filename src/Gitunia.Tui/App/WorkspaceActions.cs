using Gitunia.Core.Keymap;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the workspace-level actions (tabs, pinning, the repo picker) to workspace and picker commands. Anything else is
/// left to the <see cref="ActionRouter"/>: <see cref="TryExecute"/> returns false for actions it does not own.
/// </summary>
public sealed class WorkspaceActions(WorkspaceHost host, PickerController picker, Action quit)
{
    private const string GoToPrefix = "tab.go-to.";

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (picker.IsOpen && TryExecutePicker(action))
        {
            return true;
        }

        if (action == ActionIds.PickerOpen)
        {
            // An open picker stays as it is (a second click on "+" must not drop the query or the locate mode).
            if (!picker.IsOpen)
            {
                picker.Open();
            }
        }
        else if (action == ActionIds.TabNext)
        {
            SwitchTab(count is { } position ? new GoToTabCommand(position) : new NextTabCommand());
        }
        else if (action == ActionIds.TabPrevious)
        {
            SwitchTab(new PreviousTabCommand());
        }
        else if (action == ActionIds.TabLastActive)
        {
            SwitchTab(new ActivatePreviousTabCommand());
        }
        else if (action.Value.StartsWith(GoToPrefix, StringComparison.Ordinal) && int.TryParse(action.Value[GoToPrefix.Length..], out var number))
        {
            SwitchTab(new GoToTabCommand(number));
        }
        else if (action == ActionIds.TabClose)
        {
            CloseActiveTab();
        }
        else if (action == ActionIds.RepoPin)
        {
            PinActiveRepository();
        }
        else if (action == ActionIds.GraphOpenDetails && host.Workspace.ActiveTab is { IsMissing: true } missing)
        {
            // Enter on a missing tab offers to locate the repository: the picker starts in the folder that used to hold it.
            picker.Open(missing.Id, Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(missing.Location.RootPath)) is { Length: > 0 } parent ? parent + Path.DirectorySeparatorChar : string.Empty);
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool TryExecutePicker(ActionId action)
    {
        if (action == ActionIds.Cancel)
        {
            if (host.Workspace.Tabs.IsEmpty)
            {
                quit();
            }
            else
            {
                picker.Close();
            }
        }
        else if (action == ActionIds.PickerNext)
        {
            picker.Apply(new MoveHighlightCommand(1));
        }
        else if (action == ActionIds.PickerPrevious)
        {
            picker.Apply(new MoveHighlightCommand(-1));
        }
        else if (action == ActionIds.PickerAccept || action == ActionIds.InputAccept)
        {
            picker.Accept();
        }
        else if (action == ActionIds.PickerCompletePath)
        {
            picker.Apply(new CompletePathCommand());
        }
        else if (action == ActionIds.PickerPin)
        {
            picker.TogglePin();
        }
        else if (action == ActionIds.PickerRemoveRecent)
        {
            picker.RemoveRecent();
        }
        else
        {
            return false;
        }

        return true;
    }

    private void SwitchTab(WorkspaceCommand command)
    {
        if (host.Workspace.Tabs.IsEmpty)
        {
            return;
        }

        picker.Close();
        host.Dispatch(command);
    }

    private void CloseActiveTab()
    {
        if (host.Workspace.ActiveTabId is not { } activeId)
        {
            return;
        }

        host.Dispatch(new CloseTabCommand(activeId));
        if (host.Workspace.Tabs.IsEmpty)
        {
            picker.Open();
        }
    }

    private void PinActiveRepository()
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            Message = "no repository to pin";
            return;
        }

        var wasPinned = host.Workspace.IsPinned(tab.Location.RootPath);
        host.Dispatch(new TogglePinCommand(tab.Location.RootPath));
        Message = wasPinned ? "unpinned " + tab.DisplayName : "pinned " + tab.DisplayName + ": it stays at the top of the repository picker";
    }
}
