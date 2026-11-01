using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Input;

/// <summary>Takes a key typed in Insert or Command mode while its text field is open.</summary>
public interface ITextKeyHandler
{
    /// <summary>Handles the key; true when it was consumed.</summary>
    bool HandleTextKey(KeyEvent key);
}
