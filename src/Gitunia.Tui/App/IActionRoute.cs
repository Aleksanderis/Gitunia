using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.App;

/// <summary>Runs an action as if its key had been pressed, for the handlers that pass a key on to another one.</summary>
public interface IActionRoute
{
    /// <summary>Runs the action.</summary>
    /// <param name="action">The action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    void Route(ActionId action, int? count);
}
