using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

/// <summary>
/// The one key table for editing inside a line, shared by <see cref="PromptTextEditor"/> and <see cref="MessageEditor"/>
/// so a key is added once: arrows, Home and End, word steps (<c>Ctrl</c> or <c>Alt</c> with the arrows,
/// <c>Alt-b</c>, <c>Alt-f</c>), <c>Ctrl-a</c> and <c>Ctrl-e</c>, and the deleting keys (Backspace, Delete, <c>Ctrl-u</c>,
/// <c>Ctrl-k</c>, <c>Ctrl-w</c>, <c>Alt-d</c>). Keys that only exist in several lines (Enter, Up, Down, Tab,
/// <c>Ctrl-Home</c> and <c>Ctrl-End</c> as document edges) are the multi-line editor's own.
/// </summary>
public static class LineEditKeys
{
    /// <summary>The action of a normalized key, or null when the key is not a line-editing key.</summary>
    /// <param name="key">The key, after <see cref="KeyEvent.Normalize"/>.</param>
    public static LineEditAction? Resolve(KeyEvent key) => key.Modifiers switch
    {
        KeyModifiers.None or KeyModifiers.Shift => Plain(key.Name),
        KeyModifiers.Ctrl => Control(key.Name),
        KeyModifiers.Alt => Alt(key.Name),
        _ => null,
    };

    /// <summary>Whether the action deletes text (with a selection it deletes the selection instead).</summary>
    /// <param name="action">The action.</param>
    public static bool Deletes(LineEditAction action) =>
        action is LineEditAction.Backspace or LineEditAction.Delete or LineEditAction.DeleteToStart
            or LineEditAction.DeleteToEnd or LineEditAction.DeleteWordBack or LineEditAction.DeleteWordForward;

    private static LineEditAction? Plain(string name) => name switch
    {
        KeyNames.Backspace => LineEditAction.Backspace,
        KeyNames.Delete => LineEditAction.Delete,
        KeyNames.Left => LineEditAction.Left,
        KeyNames.Right => LineEditAction.Right,
        KeyNames.Home => LineEditAction.LineStart,
        KeyNames.End => LineEditAction.LineEnd,
        _ => null,
    };

    private static LineEditAction? Control(string name) => name switch
    {
        "a" or KeyNames.Home => LineEditAction.LineStart,
        "e" or KeyNames.End => LineEditAction.LineEnd,
        "u" => LineEditAction.DeleteToStart,
        "k" => LineEditAction.DeleteToEnd,
        "w" or KeyNames.Backspace => LineEditAction.DeleteWordBack,
        KeyNames.Delete => LineEditAction.DeleteWordForward,
        KeyNames.Left => LineEditAction.WordLeft,
        KeyNames.Right => LineEditAction.WordRight,
        _ => null,
    };

    // Alt-arrows too: many terminals report Ctrl-arrows as Alt-arrows.
    private static LineEditAction? Alt(string name) => name switch
    {
        "b" or KeyNames.Left => LineEditAction.WordLeft,
        "f" or KeyNames.Right => LineEditAction.WordRight,
        "d" => LineEditAction.DeleteWordForward,
        KeyNames.Backspace => LineEditAction.DeleteWordBack,
        _ => null,
    };
}
