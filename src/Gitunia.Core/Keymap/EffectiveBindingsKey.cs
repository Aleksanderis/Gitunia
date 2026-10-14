namespace Gitunia.Core.Keymap;

/// <summary>Cache key for the effective bindings of a mode and view.</summary>
/// <param name="Mode">The key mode.</param>
/// <param name="View">The view name.</param>
public readonly record struct EffectiveBindingsKey(KeyMode Mode, string View);
