using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Input;

/// <summary>
/// Takes an action when it is its own (<c>InputRouting</c> in Core decides the order they are asked in): true means the
/// action was taken and nobody else is asked.
/// </summary>
public interface IActionHandler
{
    /// <param name="action">The action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    bool TryExecute(ActionId action, int? count);
}
