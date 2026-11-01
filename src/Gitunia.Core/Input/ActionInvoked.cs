using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Input;

/// <summary>
/// An action id from a key, a mouse gesture, the palette, a menu or a handler that passes a key on. The reducer picks
/// the handlers that may take it, in order (<see cref="InputRouting"/>).
/// </summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The numeric prefix, null when none was typed.</param>
/// <param name="Focus">What the terminal layer knows about the focus.</param>
public sealed record ActionInvoked(ActionId Action, int? Count, InputFocus Focus) : InputCommand;
