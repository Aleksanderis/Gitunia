using Gitunia.Core.Keymap;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// Application-level key handling: every Terminal.Gui key press goes through the keymap engine, and a completed
/// binding is handed to <paramref name="execute"/> as an action id with its count. Views never see keys the keymap uses.
/// </summary>
public sealed class KeyController(KeymapEngine engine, Action<ActionId, int?> execute)
{
    private KeymapState state = KeymapState.Initial;

    /// <summary>Raised after every key the keymap looked at, so the status bar can show the pending chord.</summary>
    public event Action? PendingChanged;

    /// <summary>Raised for keys typed in a text mode (Insert, Command): printable text and unbound named keys such as Backspace.</summary>
    public event Action<KeyEvent>? TextKeyPressed;

    public KeyMode Mode => state.Mode;

    public KeymapState State => state;

    /// <summary>Switches the input mode, dropping any pending chord or count.</summary>
    public void SetMode(KeyMode mode)
    {
        state = state.WithMode(mode);
        PendingChanged?.Invoke();
    }

    /// <summary>The unfinished count and chord, such as <c>5g</c>; empty when nothing is pending.</summary>
    public string PendingText => (state.Count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
        + string.Concat((state.Pending.IsDefault ? [] : state.Pending).Select(key => key.ToString()));

    /// <param name="key">The Terminal.Gui key press.</param>
    /// <param name="view">The focused view's name in the keymap (for example <c>Graph</c>).</param>
    /// <returns>True when the key was consumed; false lets the focused widget have it.</returns>
    public bool Handle(Key key, string view)
    {
        if (!KeyTranslator.TryTranslate(key, out var keyEvent))
        {
            return false;
        }

        var result = engine.Process(state, keyEvent, view);
        state = result.State;
        if (result is { Outcome: KeyOutcome.Action, Action: { } action })
        {
            execute(action, result.Count);
        }

        if (result.Outcome == KeyOutcome.TextInput || (result.Outcome == KeyOutcome.Unbound && state.Mode == KeyMode.Command))
        {
            TextKeyPressed?.Invoke(keyEvent);
        }

        PendingChanged?.Invoke();
        return result.Outcome != KeyOutcome.Unbound || state.Mode == KeyMode.Command;
    }
}
