using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Creation;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App.Creation;

/// <summary>
/// The view side of clone and init from the picker: turns Alt-c, Alt-i, the cancel chip and Esc into
/// <see cref="CreationCommand"/>s and reads the progress line of the running clone from the shell state. The questions
/// it asks are shell prompts, so it draws and edits nothing itself.
/// </summary>
/// <param name="host">Holds the state and takes the commands.</param>
public sealed class CreationInput(Dispatcher host) : IActionHandler
{
    /// <summary>Whether a clone runs (the status bar shows its progress and a cancel chip).</summary>
    public bool IsCloning => host.State.Shell.Creation.RunningClone is not null;

    /// <summary>The status bar line of the running clone, empty when none.</summary>
    public string ProgressLine => host.State.Shell.Creation.RunningClone?.ProgressLine ?? string.Empty;

    /// <summary>Handles the clone and init keys while the picker is open and the cancel of a running clone.</summary>
    /// <param name="action">The bound action.</param>
    /// <returns>True when the action was consumed.</returns>
    public bool TryExecute(ActionId action)
    {
        var shell = host.State.Shell;
        if (action == ActionIds.RemoteCancel)
        {
            return TryCancel();
        }

        if (action != ActionIds.PickerClone && action != ActionIds.PickerInit)
        {
            return false;
        }

        if (!shell.Picker.IsOpen)
        {
            return false;
        }

        if (shell.Prompt is null)
        {
            var kind = action == ActionIds.PickerClone ? CreationKind.Clone : CreationKind.Init;
            host.Dispatch(new CreationRequested(kind));
        }

        return true;
    }

    bool IActionHandler.TryExecute(ActionId action, int? count) => TryExecute(action);

    /// <summary>Esc cancels a running clone; the caller asks only after every other holder of Esc passed.</summary>
    /// <param name="action">The bound action.</param>
    public bool TryCancelWithEscape(ActionId action) => action == ActionIds.Cancel && TryCancel();

    private bool TryCancel()
    {
        if (!IsCloning)
        {
            return false;
        }

        host.Dispatch(new CloneCancelRequested());

        return true;
    }
}
