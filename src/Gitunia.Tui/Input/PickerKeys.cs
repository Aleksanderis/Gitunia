using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Input;

/// <summary>
/// The keys that move the highlight of a type-to-filter picker. Letters filter, so the vim-style moves are
/// <c>Ctrl-j</c>/<c>Ctrl-k</c> (<c>Ctrl-p</c> opens the palette, so the readline pair is not used) next to <c>Up</c>, <c>Down</c> and <c>Tab</c>.
/// </summary>
public static class PickerKeys
{
    /// <summary>True for a move key; <paramref name="step"/> is -1 (up) or 1 (down).</summary>
    public static bool IsMove(KeyEvent key, out int step)
    {
        step = 0;
        if (key.Modifiers == KeyModifiers.None)
        {
            step = key.Name switch
            {
                KeyNames.Up => -1,
                KeyNames.Down or KeyNames.Tab => 1,
                _ => 0,
            };
        }
        else if (key.Modifiers == KeyModifiers.Ctrl)
        {
            step = key.Name switch
            {
                "k" => -1,
                "j" => 1,
                _ => 0,
            };
        }

        return step != 0;
    }
}
