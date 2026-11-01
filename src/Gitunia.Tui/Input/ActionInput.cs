using Gitunia.App;
using Gitunia.Core.Input;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Input;

/// <summary>
/// The one way into the app for a key, a mouse gesture, a palette or menu choice and a handler that passes a key on: the
/// action becomes an <see cref="ActionInvoked"/> command with the focus the terminal layer sees, and the reducer picks
/// who handles it (<see cref="InputRouting"/>).
/// </summary>
/// <param name="host">Takes the command.</param>
/// <param name="focus">Tells which overlay is open and which list has the focus.</param>
public sealed class ActionInput(Dispatcher host, IInputFocusSource focus) : IActionRoute
{
    /// <summary>Runs the action as if its key had been pressed.</summary>
    /// <param name="action">The action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public void Route(ActionId action, int? count) => host.Dispatch(new ActionInvoked(action, count, focus.Current()));
}
