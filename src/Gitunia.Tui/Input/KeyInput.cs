using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Overlays;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>Takes the key presses and pastes of the terminal: Ctrl-c quits, everything else goes through the keymap.</summary>
/// <param name="host">Tells whether a message editor is open.</param>
/// <param name="keys">The keymap engine and its mode.</param>
/// <param name="quit">Quits on Ctrl-c.</param>
/// <param name="focus">Which view has the keys.</param>
/// <param name="editors">The editors, which keep line breaks of a paste.</param>
public sealed class KeyInput(
    Dispatcher host,
    KeyController keys,
    IQuitRequests quit,
    KeyFocus focus,
    EditorOverlays editors)
{
    /// <summary>Handles a key press; sets <see cref="Key.Handled"/> when the app took it.</summary>
    /// <param name="sender">The terminal.</param>
    /// <param name="key">The key press.</param>
    public void OnKeyDown(object? sender, Key key) => key.Handled = HandleKey(key);

    /// <summary>
    /// A bracketed paste types its text, key by key, into whatever text input has Insert or Command mode; elsewhere it
    /// is ignored.
    /// </summary>
    /// <param name="sender">The terminal.</param>
    /// <param name="paste">The paste.</param>
    public void OnPaste(object? sender, PasteEventArgs paste)
    {
        if (string.IsNullOrEmpty(paste.Text))
        {
            return;
        }

        var multiLine = CommitRules.IsEditing(host.State) || editors.Rebase.IsEditingMessage || editors.Conflict.IsEditing;
        paste.Handled = keys.Paste(paste.Text, multiLine, focus.ViewName);
    }

    private bool HandleKey(Key key)
    {
        if (ForceQuitKey.IsMatch(key))
        {
            quit.Force();

            return true;
        }

        var isPickerShowing = focus.IsPickerShowing;

        return keys.Handle(key, focus.ViewName()) || isPickerShowing;
    }
}
