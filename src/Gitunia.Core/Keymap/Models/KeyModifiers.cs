namespace Gitunia.Core.Keymap.Models;

/// <summary>Modifier keys held during a key press.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,
}
