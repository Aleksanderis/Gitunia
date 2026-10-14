using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// Application-level key handling: every Terminal.Gui key press goes through the keymap engine, and a completed
/// binding is handed to <paramref name="execute"/> as an action id with its count. Views never see keys the keymap uses.
/// </summary>
public sealed class KeyController(KeymapEngine engine, Action<ActionId, int?> execute)
{
    private KeymapState _state = KeymapState.Initial;

    /// <summary>Raised after every key the keymap looked at, so the status bar can show the pending chord.</summary>
    public event Action? PendingChanged;

    /// <summary>Raised for keys typed in a text mode (Insert, Command): printable text and unbound named keys such as Backspace.</summary>
    public event Action<KeyEvent>? TextKeyPressed;

    public KeyMode Mode => _state.Mode;

    /// <summary>
    /// Gets the keys typed in Insert or Command mode that the keymap does not bind (printable text, <c>Backspace</c>,
    /// <c>Ctrl-u</c> ...); returns true when it consumed the key.
    /// </summary>
    public Func<KeyEvent, bool>? TextKeyHandler { get; set; }

    public KeymapState State => _state;

    /// <summary>Switches the input mode, dropping any pending chord or count.</summary>
    public void SetMode(KeyMode mode)
    {
        _state = _state.WithMode(mode);
        PendingChanged?.Invoke();
    }

    /// <summary>The unfinished count and chord, such as <c>5g</c>; empty when nothing is pending.</summary>
    public string PendingText => (_state.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
        + string.Concat((_state.Pending.IsDefault ? [] : _state.Pending).Select(key => key.ToString()));

    /// <param name="key">The Terminal.Gui key press.</param>
    /// <param name="view">The focused view's name in the keymap (for example <c>Graph</c>).</param>
    /// <returns>True when the key was consumed; false lets the focused widget have it.</returns>
    public bool Handle(Key key, string view)
    {
        if (!KeyTranslator.TryTranslate(key, out var keyEvent))
        {
            return false;
        }

        var result = engine.Process(_state, keyEvent, view);
        _state = result.State;
        if (result is { Outcome: KeyOutcome.Action, Action: { } action })
        {
            execute(action, result.Count);
        }
        else if (result.Outcome is KeyOutcome.TextInput or KeyOutcome.Unbound
            && _state.Mode is KeyMode.Insert or KeyMode.Command
            && TextKeyHandler?.Invoke(keyEvent) == true)
        {
            PendingChanged?.Invoke();
            return true;
        }

        if (result.Outcome == KeyOutcome.TextInput || (result.Outcome == KeyOutcome.Unbound && _state.Mode is KeyMode.Command or KeyMode.Insert))
        {
            TextKeyPressed?.Invoke(keyEvent);
        }

        PendingChanged?.Invoke();
        return result.Outcome != KeyOutcome.Unbound || _state.Mode is KeyMode.Command or KeyMode.Insert;
    }
}
