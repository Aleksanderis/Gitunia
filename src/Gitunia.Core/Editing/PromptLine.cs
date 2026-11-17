namespace Gitunia.Core.Editing;

/// <summary>
/// A line of the status bar that holds an editable text: fixed words around the typed text, and where the caret is.
/// Core builds it as data, the renderer draws the caret as a cursor cell (<see cref="PromptCaret.Split"/>), so no
/// caret glyph is ever part of the text.
/// </summary>
/// <param name="Prefix">Fixed text before the typed text, such as <c>/</c> or <c>jump </c>.</param>
/// <param name="Text">The typed text.</param>
/// <param name="Caret">The caret column in <paramref name="Text"/> (<see cref="PromptCaret.End"/> for the end); null when the text is not being edited (no cursor).</param>
/// <param name="Suffix">Fixed text after the typed text, such as the match count.</param>
public sealed record PromptLine(string Prefix, string Text, int? Caret = null, string Suffix = "")
{
    /// <summary>No line.</summary>
    public static PromptLine Empty { get; } = new(string.Empty, string.Empty);

    /// <summary>A line around the text of an edited field, with the caret where the field has it.</summary>
    /// <param name="prefix">Fixed text before the field.</param>
    /// <param name="field">The text and caret.</param>
    /// <param name="suffix">Fixed text after the field.</param>
    public static PromptLine Editing(string prefix, TextField field, string suffix = "") =>
        new(prefix, field.Text, field.Caret, suffix);

    /// <summary>Whether there is nothing to show.</summary>
    public bool IsEmpty => Plain.Length == 0;

    /// <summary>The whole line as text, without any caret.</summary>
    public string Plain => $"{Prefix}{Text}{Suffix}";

    /// <summary>A line of fixed text and no field.</summary>
    /// <param name="text">The text.</param>
    public static implicit operator PromptLine(string text) => new(text, string.Empty);

    /// <summary>The line with <paramref name="suffix"/> added after the current suffix.</summary>
    /// <param name="suffix">Text to append.</param>
    public PromptLine WithSuffix(string suffix) => this with { Suffix = $"{Suffix}{suffix}" };

    /// <summary>The line as text.</summary>
    public override string ToString() => Plain;
}
