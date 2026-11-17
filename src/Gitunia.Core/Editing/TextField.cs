using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

/// <summary>
/// An editable line of text: the text and the caret. Editing is
/// <see cref="PromptTextEditor"/>, so every field that holds a <see cref="TextField"/> gets the same keys. Immutable
/// and pure; the renderer draws it with <c>CaretField</c>.
/// </summary>
/// <param name="Text">The typed text.</param>
/// <param name="Caret">The caret column in <paramref name="Text"/> (<see cref="PromptCaret.End"/> for the end).</param>
public sealed record TextField(string Text = "", int Caret = PromptCaret.End)
{
    /// <summary>A field holding <paramref name="text"/> with the caret at its end.</summary>
    /// <param name="text">The text.</param>
    public static TextField Of(string text) => new(text);

    /// <summary>A field holding <paramref name="text"/> with the caret at its end.</summary>
    /// <param name="text">The text.</param>
    public static implicit operator TextField(string text) => Of(text);

    /// <summary>The caret limited to the text.</summary>
    public int CaretColumn => PromptCaret.Clamp(Text, Caret);

    /// <summary>The field after a key, or null when the key does not edit or move, or changes nothing.</summary>
    /// <param name="key">The key.</param>
    public TextField? Apply(KeyEvent key) =>
        PromptTextEditor.Apply(Text, Caret, key) is { } edit ? this with { Text = edit.Text, Caret = edit.Caret } : null;

    /// <summary>The field after inserting <paramref name="inserted"/> at the caret (a paste), or null for empty text.</summary>
    /// <param name="inserted">The text.</param>
    public TextField? Insert(string inserted) =>
        PromptTextEditor.Insert(Text, Caret, inserted) is { } edit ? this with { Text = edit.Text, Caret = edit.Caret } : null;
}
