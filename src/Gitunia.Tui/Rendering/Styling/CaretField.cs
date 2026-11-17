using Gitunia.Core.Editing;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// An editable one-line text with its caret, drawn like the message editor's cursor: the character under the caret (or
/// a blank at the end) is a caret cell, so the text is not pushed apart and the cursor shows without colour too. The
/// roles come from <see cref="FieldRoles"/>, never from the caller, so every field looks the same.
/// </summary>
/// <param name="Prefix">Fixed text before the field, such as "/" or the prompt glyph.</param>
/// <param name="Text">The typed text.</param>
/// <param name="Caret">The caret column in <paramref name="Text"/> (<see cref="PromptCaret.End"/> for the end).</param>
/// <param name="Suffix">Fixed text after the field, such as a match count.</param>
/// <param name="Placeholder">Hint drawn after the caret cell while the text is empty.</param>
public sealed record CaretField(
    string Prefix,
    string Text,
    int Caret,
    string Suffix = "",
    string Placeholder = "")
{
    /// <summary>A field around the text and caret of <paramref name="field"/>.</summary>
    /// <param name="prefix">Fixed text before the field.</param>
    /// <param name="field">The text and caret.</param>
    /// <param name="suffix">Fixed text after the field.</param>
    /// <param name="placeholder">Hint drawn while the text is empty.</param>
    public static CaretField Of(string prefix, TextField field, string suffix = "", string placeholder = "") =>
        new(prefix, field.Text, field.Caret, suffix, placeholder);

    /// <summary>Appends the field to <paramref name="builder"/>: the prefix, the text around the caret cell cut to the remaining columns (the prefix and suffix come first), the suffix.</summary>
    /// <param name="builder">The line.</param>
    /// <param name="roles">The roles of the surface the field sits on.</param>
    public void AppendTo(LineBuilder builder, FieldRoles roles)
    {
        builder.Append(Prefix, roles.Prefix);
        var room = Math.Max(1, builder.Remaining - TextWidth.Of(Suffix));
        var split = PromptCaret.Split(Text, Caret, room);
        var cell = split.Cell.Length == 1 && char.IsControl(split.Cell[0]) ? PromptCaret.EndCell : split.Cell;
        builder
            .Append(split.Before, roles.TypedText)
            .Append(cell, roles.Caret)
            .Append(split.After, roles.TypedText);
        if (Text.Length == 0 && Placeholder.Length > 0)
        {
            builder.Append($" {Placeholder}", roles.Placeholder);
        }

        builder.Append(Suffix, roles.Prefix);
    }
}
