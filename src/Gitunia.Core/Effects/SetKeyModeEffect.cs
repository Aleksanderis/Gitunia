using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Effects;

/// <summary>Switch the keymap mode (Insert while a prompt takes typed text). The runner turns it into a
/// <c>SetKeyModeCommand</c>, which changes <c>ShellState.Mode</c>.</summary>
/// <param name="Mode">The mode to switch to.</param>
public sealed record SetKeyModeEffect(KeyMode Mode) : AppEffect;
