namespace Gitunia.Core.Keymap.Models;

/// <summary>The place a binding occupies: mode, view and key sequence. Two bindings in one slot are duplicates.</summary>
/// <param name="Mode">The key mode.</param>
/// <param name="View">The view name.</param>
/// <param name="Sequence">The key sequence.</param>
public readonly record struct BindingSlot(KeyMode Mode, string View, KeySequence Sequence);
