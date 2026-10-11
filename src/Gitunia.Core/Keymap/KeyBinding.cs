namespace Gitunia.Core.Keymap;

/// <summary>One row of the keymap: in <see cref="Mode"/> and <see cref="View"/>, <see cref="Sequence"/> triggers <see cref="Action"/>.</summary>
/// <param name="Mode">Mode in which the binding is active.</param>
/// <param name="View">View name such as <c>Graph</c>, or <c>Global</c> for every view.</param>
/// <param name="Sequence">Keys that trigger the action.</param>
/// <param name="Action">The action to run.</param>
public sealed record KeyBinding(KeyMode Mode, string View, KeySequence Sequence, ActionId Action);
