using Gitunia.Core.Editing;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>
/// An editable one-line text with its caret, drawn like the message editor's cursor: the character under the caret (or
/// a blank at the end) is a <see cref="SegmentRole.Cursor"/> cell, so the text is not pushed apart and the cursor shows
/// without colour too.
/// </summary>
/// <param name="Prefix">Fixed text before the field, such as "/" or the prompt glyph.</param>
/// <param name="Text">The typed text.</param>
/// <param name="Caret">The caret column in <paramref name="Text"/> (<see cref="PromptCaret.End"/> for the end).</param>
/// <param name="Suffix">Fixed text after the field, such as a match count.</param>
public sealed record CaretField(string Prefix, string Text, int Caret, string Suffix = "")
{
    /// <summary>Appends the field to <paramref name="builder"/>: the prefix, the text around the cursor cell cut to the remaining columns (the prefix and suffix come first), the suffix.</summary>
    /// <param name="builder">The line.</param>
    /// <param name="role">Role of the prefix, the text and the suffix.</param>
    /// <param name="textRole">Role of the text; defaults to <paramref name="role"/>.</param>
    public void AppendTo(LineBuilder builder, SegmentRole role, SegmentRole? textRole = null)
    {
        builder.Append(Prefix, role);
        var room = Math.Max(1, builder.Remaining - TextWidth.Of(Suffix));
        var split = PromptCaret.Split(Text, Caret, room);
        var cell = split.Cell.Length == 1 && char.IsControl(split.Cell[0]) ? PromptCaret.EndCell : split.Cell;
        builder
            .Append(split.Before, textRole ?? role)
            .Append(cell, SegmentRole.Cursor)
            .Append(split.After, textRole ?? role)
            .Append(Suffix, role);
    }
}
