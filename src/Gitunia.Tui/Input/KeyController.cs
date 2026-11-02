using System.Globalization;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// Application-level key handling: every Terminal.Gui key press goes through the keymap engine, and a completed
/// binding is handed to the execute callback as an action id with its count. Views never see keys the keymap uses.
/// The input mode is app state (<c>ShellState.Mode</c>): it changes by dispatching a mode command, and this controller
/// follows it, dropping the pending chord and count on every switch.
/// </summary>
public sealed class KeyController
{
    private readonly KeymapEngine _engine;
    private readonly IActionRoute _route;
    private readonly IKeyInterceptor? _interceptor;
    private readonly ITextKeyHandler? _textKeys;
    private readonly Dispatcher _host;
    private KeymapState _state = KeymapState.Initial;
    private long _seenModeChanges;

    /// <param name="engine">The keymap engine.</param>
    /// <param name="route">Receives a completed binding as an action id with its count.</param>
    /// <param name="host">Holds the input mode.</param>
    /// <param name="interceptor">Gets every key before the keymap does (the repeat mode); null for none.</param>
    /// <param name="textKeys">
    /// Gets the keys typed in Insert or Command mode that the keymap does not bind (printable text, <c>Backspace</c>,
    /// <c>Ctrl-u</c> ...); null for none.
    /// </param>
    public KeyController(
        KeymapEngine engine,
        IActionRoute route,
        Dispatcher host,
        IKeyInterceptor? interceptor = null,
        ITextKeyHandler? textKeys = null)
    {
        _engine = engine;
        _route = route;
        _host = host;
        _interceptor = interceptor;
        _textKeys = textKeys;
        _seenModeChanges = host.State.Shell.ModeChanges;
        _state = _state.WithMode(host.State.Shell.Mode);
        host.Changed += FollowMode;
    }

    /// <summary>Raised after every key the keymap looked at, so the status bar can show the pending chord.</summary>
    public event Action? PendingChanged;

    /// <summary>Raised for keys typed in a text mode (Insert, Command): printable text and unbound named keys such as Backspace.</summary>
    public event Action<KeyEvent>? TextKeyPressed;

    public KeyMode Mode => _state.Mode;

    private bool IsTextMode => _state.Mode is KeyMode.Insert or KeyMode.Command;

    public KeymapState State => _state;

    /// <summary>The unfinished count and chord, such as <c>5g</c>; empty when nothing is pending.</summary>
    public string PendingText => (_state.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
        + string.Concat((_state.Pending.IsDefault ? [] : _state.Pending).Select(KeyFormatter.Format));

    /// <param name="key">The Terminal.Gui key press.</param>
    /// <param name="view">The focused view's name in the keymap (for example <c>Graph</c>).</param>
    /// <returns>True when the key was consumed; false lets the focused widget have it.</returns>
    public bool Handle(Key key, string view)
    {
        return KeyTranslator.TryTranslate(key, out var keyEvent) && Handle(keyEvent, view);
    }

    /// <summary>
    /// Types a bracketed paste, key by key, while Insert or Command mode lasts: it stops as soon as a key leaves the
    /// text mode (a quick jump that matched, an accepted prompt), so the rest never runs as Normal-mode keys.
    /// </summary>
    /// <param name="text">The pasted text.</param>
    /// <param name="multiLine">True when a message editor owns the input and keeps line breaks.</param>
    /// <param name="view">The focused view's name in the keymap, asked for every key (an overlay may close).</param>
    /// <returns>True when the paste was taken (the mode was a text mode when it arrived).</returns>
    public bool Paste(string text, bool multiLine, Func<string> view)
    {
        if (!IsTextMode)
        {
            return false;
        }

        foreach (var key in PasteKeys.FromText(text, multiLine))
        {
            if (!IsTextMode)
            {
                break;
            }

            Handle(key, view());
        }

        return true;
    }

    /// <summary>Feeds an already translated key (a pasted character, see <see cref="PasteKeys"/>).</summary>
    /// <param name="keyEvent">The key.</param>
    /// <param name="view">The focused view's name in the keymap.</param>
    /// <returns>True when the key was consumed.</returns>
    public bool Handle(KeyEvent keyEvent, string view)
    {
        if (_interceptor?.TryHandle(keyEvent, _state) == true)
        {
            PendingChanged?.Invoke();

            return true;
        }

        var result = _engine.Process(_state, keyEvent, view);
        _state = result.State;
        if (result is { Outcome: KeyOutcome.Action, Action: { } action })
        {
            _route.Route(action, result.Count);
        }
        else if (result.Outcome is KeyOutcome.TextInput or KeyOutcome.Unbound
            && _state.Mode is KeyMode.Insert or KeyMode.Command
            && _textKeys?.HandleTextKey(keyEvent) == true)
        {
            PendingChanged?.Invoke();
            return true;
        }

        if (result.Outcome == KeyOutcome.TextInput || (result.Outcome == KeyOutcome.Unbound && _state.Mode is KeyMode.Command or KeyMode.Insert))
        {
            TextKeyPressed?.Invoke(keyEvent);
        }

        PendingChanged?.Invoke();

        // A confirm dialog swallows every key, so a stray one (Enter, an arrow) reaches nothing behind it.
        return result.Outcome != KeyOutcome.Unbound || _state.Mode is KeyMode.Command or KeyMode.Insert or KeyMode.Confirm;
    }

    // Every mode switch (also to the same mode) drops the pending chord or count.
    private void FollowMode()
    {
        var shell = _host.State.Shell;
        if (shell.ModeChanges == _seenModeChanges)
        {
            return;
        }

        _seenModeChanges = shell.ModeChanges;
        _state = _state.WithMode(shell.Mode);
        PendingChanged?.Invoke();
    }
}
