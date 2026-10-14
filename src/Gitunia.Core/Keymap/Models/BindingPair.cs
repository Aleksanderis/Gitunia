namespace Gitunia.Core.Keymap.Models;

/// <summary>An ordered pair of bindings, used to report each prefix shadow only once.</summary>
/// <param name="Shorter">The binding whose sequence is a prefix.</param>
/// <param name="Longer">The binding it shadows.</param>
public readonly record struct BindingPair(KeyBinding Shorter, KeyBinding Longer);
