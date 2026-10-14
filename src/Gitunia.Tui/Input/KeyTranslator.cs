using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>Turns Terminal.Gui key presses into the renderer-agnostic <see cref="KeyEvent"/> the keymap engine takes.</summary>
public static class KeyTranslator
{
    /// <summary>Returns false for presses the keymap has no use for (a bare modifier, an unknown function key).</summary>
    public static bool TryTranslate(Key key, out KeyEvent keyEvent)
    {
        keyEvent = default;
        if (key.IsModifierOnly)
        {
            return false;
        }

        var modifiers = KeyModifiers.None;
        if (key.IsCtrl)
        {
            modifiers |= KeyModifiers.Ctrl;
        }

        if (key.IsAlt)
        {
            modifiers |= KeyModifiers.Alt;
        }

        var bare = key.NoCtrl.NoAlt.NoShift;
        if (key.IsCtrl && bare.KeyCode == KeyCode.D6)
        {
            // Terminals send Ctrl-^ as 0x1E, which Terminal.Gui reports as Ctrl+6 (and a real Ctrl-6 is the same key).
            keyEvent = new KeyEvent("^", KeyModifiers.Ctrl | (key.IsAlt ? KeyModifiers.Alt : KeyModifiers.None));
            return true;
        }

        if (key.IsCtrl && bare.KeyCode == KeyCode.Enter)
        {
            // Ctrl-j is the line feed (0x0A); Terminal.Gui reports it as Ctrl+Enter, distinct from Enter (0x0D).
            keyEvent = new KeyEvent("j", KeyModifiers.Ctrl | (key.IsAlt ? KeyModifiers.Alt : KeyModifiers.None));
            return true;
        }

        var named = NamedKey(bare.KeyCode);
        if (named is not null)
        {
            keyEvent = new KeyEvent(named, modifiers | (key.IsShift ? KeyModifiers.Shift : KeyModifiers.None));
            return true;
        }

        if (!key.NoCtrl.NoAlt.TryGetPrintableRune(out var rune) || rune.Value < 0x20)
        {
            return false;
        }

        // Shift is folded into the typed text (G, not Shift-g), except together with Ctrl or Alt where it is a modifier.
        var text = rune.ToString();
        if (modifiers != KeyModifiers.None && key.IsShift)
        {
            modifiers |= KeyModifiers.Shift;
        }

        keyEvent = new KeyEvent(modifiers == KeyModifiers.None ? text : text.ToLowerInvariant(), modifiers);
        return true;
    }

    private static string? NamedKey(KeyCode code) => code switch
    {
        KeyCode.Enter => KeyNames.Enter,
        KeyCode.Esc => KeyNames.Escape,
        KeyCode.Tab => KeyNames.Tab,
        KeyCode.Backspace => KeyNames.Backspace,
        KeyCode.Delete => KeyNames.Delete,
        KeyCode.CursorUp => KeyNames.Up,
        KeyCode.CursorDown => KeyNames.Down,
        KeyCode.CursorLeft => KeyNames.Left,
        KeyCode.CursorRight => KeyNames.Right,
        KeyCode.Home => KeyNames.Home,
        KeyCode.End => KeyNames.End,
        KeyCode.PageUp => KeyNames.PageUp,
        KeyCode.PageDown => KeyNames.PageDown,
        _ => null,
    };
}
