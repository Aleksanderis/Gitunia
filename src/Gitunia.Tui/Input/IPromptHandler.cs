using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;

namespace Gitunia.Tui.Input;

/// <summary>
/// A modal prompt of the graph view (search box, jump-to-ref): it handles the actions that open, accept and cancel
/// it, receives the typed keys while Insert mode is on and describes itself for the status bar.
/// </summary>
public interface IPromptHandler
{
    /// <summary>What the prompt shows in the status bar while it is open, empty when it is not.</summary>
    string StatusLine { get; }

    /// <summary>
    /// The prompt as a centred popup (title, text field, answering keys) while it asks for text or a choice, null when
    /// it is closed or is a find-as-you-type bar that stays in the status bar (search, jump).
    /// </summary>
    PromptPopup? Popup => null;

    /// <summary>A short message about the last action (for example "No match"), empty when none.</summary>
    string Notice { get; }

    /// <summary>Runs the action when this handler owns it; false lets the next handler try.</summary>
    bool TryExecute(ActionId action, int? count);

    /// <summary>Handles a key typed in Insert mode; true when the prompt consumed it.</summary>
    bool HandleTextKey(KeyEvent key);

    /// <summary>The loaded graph changed (rows appended or loading finished).</summary>
    void OnGraphChanged();
}
