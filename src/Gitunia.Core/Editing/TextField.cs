using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Editing;

/// <summary>
/// An editable line of text: the text, the caret and the placeholder shown while it is empty. Editing is
/// <see cref="PromptTextEditor"/>, so every field that holds a <see cref="TextField"/> gets the same keys. Immutable
/// and pure; the renderer draws it with <c>CaretField</c>.
/// </summary>
/// <param name="Text">The typed text.</param>
/// <param name="Caret">The caret column in <paramref name="Text"/> (<see cref="PromptCaret.End"/> for the end).</param>
/// <param name="Placeholder">Hint shown while the text is empty; empty for none.</param>
public sealed record TextField(string Text = "", int Caret = PromptCaret.End, string Placeholder = "")
{
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

    /// <summary>The field as a line of the status bar with <paramref name="prefix"/> in front.</summary>
    /// <param name="prefix">Fixed text before the field.</param>
    /// <param name="suffix">Fixed text after the field.</param>
    public PromptLine ToLine(string prefix, string suffix = "") => new(prefix, Text, Caret, suffix);
}
