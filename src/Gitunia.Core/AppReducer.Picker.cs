using Gitunia.Core.Creation;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core;

// The repository picker, the clone and init flow, the tab and pin actions and the quit guard (#985). Each case of the
// root switch delegates here; the logic is in the feature reducers.
public static partial class AppReducer
{
    private static AppUpdate ReducePicker(AppState state, PickerCommand command)
    {
        var update = PickerSessionReducer.Reduce(state.Shell.Picker, command, state.Workspace);
        var shell = state.Shell with { Picker = update.Session };
        var effects = new List<AppEffect>(update.Effects);

        // The picker is the keyboard's while it is open; a question it asked (clone, init) goes with it.
        if (update.Session.IsOpen != state.Shell.Picker.IsOpen)
        {
            shell = PickerModeChanged(shell, update.Session.IsOpen, effects);
        }

        return Follow(state with { Shell = shell }, effects, update.Next);
    }

    private static ShellState PickerModeChanged(ShellState shell, bool isOpen, List<AppEffect> effects)
    {
        var changed = shell;
        if (!isOpen && shell.Prompt is { OnAccepted: ICreationPromptResult })
        {
            changed = ShellReducer.Reduce(shell, new PromptCancelledCommand()).State;
        }

        if (changed.Confirm is null && changed.Output is null)
        {
            effects.Add(new SetKeyModeEffect(isOpen ? KeyMode.Insert : KeyMode.Normal));
        }

        return changed;
    }

    private static AppUpdate ReduceCreation(AppState state, CreationCommand command)
    {
        var context = new CreationContext(
            state.Shell.Picker,
            PickerSessionReducer.BaseDirectory(state.Shell.Picker, state.Workspace));
        var update = CreationReducer.Reduce(state.Shell.Creation, command, context);
        var shell = state.Shell with { Creation = update.Session };

        return Follow(state with { Shell = shell }, [.. update.Effects], update.Next);
    }

    private static AppUpdate ReduceWorkspaceAction(AppState state, WorkspaceActionPressed pressed)
    {
        var update = WorkspaceActionReducer.Reduce(state.Shell.TabActions, pressed, state);
        var shell = state.Shell with { TabActions = update.Session };

        return Follow(state with { Shell = shell }, [], update.Next);
    }

    private static AppUpdate ReduceQuit(AppState state, QuitCommand command)
    {
        var update = QuitReducer.Reduce(state.Shell, command);

        return new AppUpdate(state with { Shell = update.State }, update.Effects);
    }

    // The commands that follow are reduced in order, each on the state the one before left.
    // Settling between them is the caller's choice: Reduce settles, ReduceCommand does not.
    private static AppUpdate Follow(
        AppState state,
        List<AppEffect> effects,
        IEnumerable<AppCommand> next,
        Func<AppState, AppCommand, AppUpdate>? reduce = null)
    {
        var step = reduce ?? Reduce;
        var current = state;
        foreach (var command in next)
        {
            var then = step(current, command);
            current = then.State;
            effects.AddRange(then.Effects);
        }

        return new AppUpdate(current, [.. effects]);
    }
}
