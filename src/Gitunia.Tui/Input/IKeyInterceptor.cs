using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Input;

/// <summary>Gets every key before the keymap does (the repeat mode).</summary>
public interface IKeyInterceptor
{
    /// <summary>Handles the key before the keymap sees it.</summary>
    /// <param name="key">The key press.</param>
    /// <param name="state">The keymap state before the key.</param>
    /// <returns>True when the key was consumed.</returns>
    bool TryHandle(KeyEvent key, KeymapState state);
}
