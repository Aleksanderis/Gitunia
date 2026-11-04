using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Workspaces;

/// <summary>
/// Maps the workspace-level actions (tabs, pinning, the repository picker) to workspace and picker commands. Anything
/// else is left to the other handlers: <see cref="Owns"/> says false for actions it does not own.
/// </summary>
public static class WorkspaceActionReducer
{
    private const string PickerPrefix = "picker.";
    private const string GoToPrefix = "tab.go-to.";
    private const string NoPinNotice = "no repository to pin";

    /// <summary>Whether the action is the workspace level's: the caller dispatches it and stops looking.</summary>
    public static bool Owns(ActionId action, AppState state) => Classify(action, state) != Kind.None;

    /// <summary>Applies a pressed action.</summary>
    public static WorkspaceActionUpdate Reduce(
        WorkspaceActionSession session,
        WorkspaceActionPressed pressed,
        AppState state)
    {
        var action = pressed.Action;
        var armed = action == ActionIds.TabClose ? session.CloseArmedFor : null;
        var cleared = new WorkspaceActionSession(armed, string.Empty);

        return Classify(action, state) switch
        {
            Kind.PickerCancel => Next(cleared, new PickerCancelPressed(pressed.AnyUnsentMessage)),
            Kind.PickerNext => Next(cleared, new MoveHighlightCommand(1)),
            Kind.PickerPrevious => Next(cleared, new MoveHighlightCommand(-1)),
            Kind.PickerAccept => Next(cleared, new PickerAcceptPressed()),
            Kind.PickerComplete => Next(cleared, new CompletePathCommand()),
            Kind.PickerPin => Next(cleared, new PickerPinPressed()),
            Kind.PickerRemoveRecent => Next(cleared, new PickerRemoveRecentPressed()),
            Kind.PromptAccept => Next(cleared, new PromptAcceptedCommand()),
            Kind.PickerInert => Next(cleared),
            Kind.PickerOpen => Next(cleared, new PickerOpenRequested(null, string.Empty, pressed.Now, true)),
            Kind.TabNext => Switch(
                cleared,
                state,
                pressed.Count is { } position ? new GoToTabCommand(position) : new NextTabCommand()),
            Kind.TabPrevious => Switch(cleared, state, new PreviousTabCommand()),
            Kind.TabMoveLeft => Switch(cleared, state, new MoveTabCommand(-(pressed.Count ?? 1))),
            Kind.TabMoveRight => Switch(cleared, state, new MoveTabCommand(pressed.Count ?? 1)),
            Kind.TabLastActive => Switch(cleared, state, new ActivatePreviousTabCommand()),
            Kind.TabGoTo => Switch(cleared, state, new GoToTabCommand(GoToNumber(action))),
            Kind.TabClose => Close(cleared, pressed, state),
            Kind.TabRename => Next(cleared, new TabRenameRequested(state.Workspace.ActiveTabId!.Value)),
            Kind.Pin => Pin(cleared, state),
            Kind.Locate => Locate(cleared, pressed, state),
            _ => Next(cleared),
        };
    }

    // The action decides by state alone, so the caller can ask before it dispatches.
    private static Kind Classify(ActionId action, AppState state)
    {
        var workspace = state.Workspace;
        var pickerOpen = state.Shell.Picker.IsOpen;
        if (pickerOpen && state.Shell.Prompt is not null)
        {
            // A question of the picker (clone, init) is open: Enter and Esc belong to the prompt, the picker keys do nothing.
            if (action == ActionIds.PickerAccept)
            {
                return Kind.PromptAccept;
            }

            if (action != ActionIds.InputAccept && action != ActionIds.Cancel
                && action.Value.StartsWith(PickerPrefix, StringComparison.Ordinal))
            {
                return Kind.PickerInert;
            }
        }
        else if (pickerOpen && ClassifyPicker(action) is var picker && picker != Kind.None)
        {
            return picker;
        }

        return ClassifyWorkspace(action, workspace);
    }

    private static Kind ClassifyPicker(ActionId action)
    {
        if (action == ActionIds.Cancel)
        {
            return Kind.PickerCancel;
        }

        if (action == ActionIds.PickerNext)
        {
            return Kind.PickerNext;
        }

        if (action == ActionIds.PickerPrevious)
        {
            return Kind.PickerPrevious;
        }

        if (action == ActionIds.PickerAccept || action == ActionIds.InputAccept)
        {
            return Kind.PickerAccept;
        }

        if (action == ActionIds.PickerCompletePath)
        {
            return Kind.PickerComplete;
        }

        if (action == ActionIds.PickerPin)
        {
            return Kind.PickerPin;
        }

        return action == ActionIds.PickerRemoveRecent ? Kind.PickerRemoveRecent : Kind.None;
    }

    private static Kind ClassifyWorkspace(ActionId action, Workspace workspace)
    {
        if (action == ActionIds.PickerOpen)
        {
            return Kind.PickerOpen;
        }

        if (action == ActionIds.TabNext)
        {
            return Kind.TabNext;
        }

        if (action == ActionIds.TabPrevious)
        {
            return Kind.TabPrevious;
        }

        if (action == ActionIds.TabMoveLeft)
        {
            return Kind.TabMoveLeft;
        }

        if (action == ActionIds.TabMoveRight)
        {
            return Kind.TabMoveRight;
        }

        if (action == ActionIds.TabLastActive)
        {
            return Kind.TabLastActive;
        }

        if (action.Value.StartsWith(GoToPrefix, StringComparison.Ordinal)
            && int.TryParse(action.Value[GoToPrefix.Length..], out _))
        {
            return Kind.TabGoTo;
        }

        if (action == ActionIds.TabClose)
        {
            return Kind.TabClose;
        }

        if (action == ActionIds.TabRename && workspace.ActiveTabId is not null)
        {
            return Kind.TabRename;
        }

        if (action == ActionIds.RepositoryPin)
        {
            return Kind.Pin;
        }

        return action == ActionIds.GraphOpenDetails && workspace.ActiveTab is { IsMissing: true }
            ? Kind.Locate
            : Kind.None;
    }

    private static int GoToNumber(ActionId action) => int.Parse(action.Value[GoToPrefix.Length..]);

    private static WorkspaceActionUpdate Next(WorkspaceActionSession session, params AppCommand[] commands) =>
        new(session, [.. commands]);

    private static WorkspaceActionUpdate Switch(WorkspaceActionSession session, AppState state, WorkspaceCommand command)
    {
        if (state.Workspace.Tabs.IsEmpty)
        {
            return Next(session);
        }

        return Next(session, new PickerCloseRequested(), command);
    }

    private static WorkspaceActionUpdate Close(
        WorkspaceActionSession session,
        WorkspaceActionPressed pressed,
        AppState state)
    {
        var workspace = state.Workspace;
        if (workspace.ActiveTabId is not { } activeId)
        {
            return Next(session);
        }

        if (pressed.ActiveTabHasUnsentMessage && session.CloseArmedFor != activeId)
        {
            // The first close only says so; the same action again right after (nothing else in between) closes and drops the draft.
            var notice = $"Unsent commit message in {workspace.ActiveTab?.Title}: "
                + "close again to lose it, or commit it first";

            return Next(new WorkspaceActionSession(activeId, notice));
        }

        var reopen = new PickerOpenRequested(null, string.Empty, pressed.Now, false, true);

        return Next(session with { CloseArmedFor = null }, new CloseTabCommand(activeId), reopen);
    }

    private static WorkspaceActionUpdate Pin(WorkspaceActionSession session, AppState state)
    {
        var workspace = state.Workspace;
        if (workspace.ActiveTab is not { } tab)
        {
            return Next(session with { Notice = NoPinNotice });
        }

        var wasPinned = workspace.IsPinned(tab.Location.RootPath);
        var notice = wasPinned
            ? $"unpinned {tab.DisplayName}"
            : $"pinned {tab.DisplayName}: it stays at the top of the repository picker";

        return Next(session with { Notice = notice }, new TogglePinCommand(tab.Location.RootPath));
    }

    // Enter on a missing tab offers to locate the repository: the picker starts in the folder that used to hold it.
    private static WorkspaceActionUpdate Locate(
        WorkspaceActionSession session,
        WorkspaceActionPressed pressed,
        AppState state)
    {
        var missing = state.Workspace.ActiveTab!;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(missing.Location.RootPath));
        var query = parent is { Length: > 0 } ? parent + Path.DirectorySeparatorChar : string.Empty;

        return Next(session, new PickerOpenRequested(missing.Id, query, pressed.Now));
    }

    private enum Kind
    {
        None,
        PickerCancel,
        PickerNext,
        PickerPrevious,
        PickerAccept,
        PickerComplete,
        PickerPin,
        PickerRemoveRecent,
        PickerInert,
        PromptAccept,
        PickerOpen,
        TabNext,
        TabPrevious,
        TabLastActive,
        TabMoveLeft,
        TabMoveRight,
        TabGoTo,
        TabClose,
        TabRename,
        Pin,
        Locate,
    }
}
