using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Effects;

/// <summary>Switch the keymap mode (Insert while a prompt takes typed text). Moves into state with the key-mode
/// migration (#970).</summary>
/// <param name="Mode">The mode to switch to.</param>
public sealed record SetKeyModeEffect(KeyMode Mode) : AppEffect;
