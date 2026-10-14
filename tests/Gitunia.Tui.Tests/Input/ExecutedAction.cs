using Gitunia.Core.Keymap;

namespace Gitunia.Tui.Tests.Input;

/// <summary>An action the mouse controller asked to run, with its repeat count.</summary>
/// <param name="Action">The action.</param>
/// <param name="Count">The count, or null.</param>
public sealed record ExecutedAction(ActionId Action, int? Count);
