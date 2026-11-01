using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Overlays;

namespace Gitunia.Tui.App;

/// <summary>
/// Maps the workspace-level actions (tabs, pinning, the repository picker) to workspace and picker commands. Anything else is
/// left to the <see cref="ActionRouter"/>: <see cref="TryExecute"/> returns false for actions it does not own.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="picker">The repository picker.</param>
/// <param name="quit">Stops the program.</param>
/// <param name="hasUnsentMessage">
/// Tells whether a tab holds a commit message that was not committed (closing it needs a second step); null for never.
/// </param>
public sealed class WorkspaceActions(
    WorkspaceHost host,
    PickerController picker,
    Action quit,
    Func<TabId, bool>? hasUnsentMessage = null)
{
    private const string GoToPrefix = "tab.go-to.";

    private TabId? _closeArmedFor;

    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Message { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Message = string.Empty;
        if (action != ActionIds.TabClose)
        {
            _closeArmedFor = null;
        }

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
        else if (action == ActionIds.RepositoryPin)
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

        if (hasUnsentMessage?.Invoke(activeId) == true && _closeArmedFor != activeId)
        {
            // The first close only says so; the same action again right after (nothing else in between) closes and drops the draft.
            _closeArmedFor = activeId;
            Message = $"Unsent commit message in {host.Workspace.ActiveTab?.Title}: "
                + "close again to lose it, or commit it first";
            return;
        }

        _closeArmedFor = null;
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
        Message = wasPinned ? $"unpinned {tab.DisplayName}" : $"pinned {tab.DisplayName}: it stays at the top of the repository picker";
    }
}
