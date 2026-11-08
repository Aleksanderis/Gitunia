using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

/// <summary>
/// Single-line text editing with a caret for prompts in Insert mode. Typing inserts at the caret; Left, Right,
/// Home, End, <c>Ctrl-a</c>, <c>Ctrl-e</c> and word movement (<c>Ctrl-Left</c>, <c>Alt-b</c>...) move it; Backspace,
/// Delete, <c>Ctrl-u</c> (to the start), <c>Ctrl-k</c> (to the end), <c>Ctrl-w</c> and <c>Alt-d</c> delete. Words
/// follow <see cref="WordBoundaries"/>, like the message box.
/// </summary>
public static class PromptTextEditor
{
    /// <summary>
    /// Applies a key; returns null when the key does not edit or move, or changes nothing (so callers only act on a
    /// real change).
    /// </summary>
    public static PromptEdit? Apply(string text, int caret, KeyEvent rawKey)
    {
        var at = PromptCaret.Clamp(text, caret);
        var key = rawKey.Normalize();
        var result = key.IsPrintable ? new PromptEdit(text.Insert(at, key.Name), at + key.Name.Length) : Edit(text, at, key);
        return result is { } edit && (edit.Text != text || edit.Caret != at) ? edit : null;
    }

    /// <summary>Inserts text at the caret and puts the caret after it; null for empty text.</summary>
    public static PromptEdit? Insert(string text, int caret, string inserted)
    {
        var at = PromptCaret.Clamp(text, caret);
        return inserted.Length == 0 ? null : new PromptEdit(text.Insert(at, inserted), at + inserted.Length);
    }

    private static PromptEdit? Edit(string text, int at, KeyEvent key)
    {
        return key.Modifiers switch
        {
            KeyModifiers.None or KeyModifiers.Shift => Plain(text, at, key.Name),
            KeyModifiers.Ctrl => Control(text, at, key.Name),
            KeyModifiers.Alt => Alt(text, at, key.Name),
            _ => null,
        };
    }

    private static PromptEdit? Plain(string text, int at, string name) => name switch
    {
        KeyNames.Backspace => DeleteTo(text, at, PromptCaret.Previous(text, at)),
        KeyNames.Delete => DeleteTo(text, at, PromptCaret.Next(text, at)),
        KeyNames.Left => new PromptEdit(text, PromptCaret.Previous(text, at)),
        KeyNames.Right => new PromptEdit(text, PromptCaret.Next(text, at)),
        KeyNames.Home => new PromptEdit(text, 0),
        KeyNames.End => new PromptEdit(text, text.Length),
        _ => null,
    };

    private static PromptEdit? Control(string text, int at, string name) => name switch
    {
        "a" or KeyNames.Home => new PromptEdit(text, 0),
        "e" or KeyNames.End => new PromptEdit(text, text.Length),
        "u" => DeleteTo(text, at, 0),
        "k" => DeleteTo(text, at, text.Length),
        "w" or KeyNames.Backspace => DeleteTo(text, at, WordBoundaries.Previous(text, at)),
        KeyNames.Delete => DeleteTo(text, at, WordBoundaries.Next(text, at)),
        KeyNames.Left => new PromptEdit(text, WordBoundaries.Previous(text, at)),
        KeyNames.Right => new PromptEdit(text, WordBoundaries.Next(text, at)),
        _ => null,
    };

    // Alt-arrows too: many terminals report Ctrl-arrows as Alt-arrows.
    private static PromptEdit? Alt(string text, int at, string name) => name switch
    {
        "b" or KeyNames.Left => new PromptEdit(text, WordBoundaries.Previous(text, at)),
        "f" or KeyNames.Right => new PromptEdit(text, WordBoundaries.Next(text, at)),
        "d" => DeleteTo(text, at, WordBoundaries.Next(text, at)),
        KeyNames.Backspace => DeleteTo(text, at, WordBoundaries.Previous(text, at)),
        _ => null,
    };

    // Deletes between the caret and another column.
    private static PromptEdit DeleteTo(string text, int at, int column)
    {
        var start = Math.Min(column, at);
        return new PromptEdit(text.Remove(start, Math.Max(column, at) - start), start);
    }
}
