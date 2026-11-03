using System.Text;
using Gitunia.Core.Text;

namespace Gitunia.Core.Visual;

/// <summary>
/// A character-wise selection in the text lines of a pane: it runs from the anchor to the cursor, in either direction, and
/// includes the character under both ends (like vim's <c>v</c>). Pure data and the text it covers.
/// </summary>
/// <param name="Anchor">Where the selection started.</param>
/// <param name="Cursor">Where its moving end is.</param>
public readonly record struct TextSelection(TextPosition Anchor, TextPosition Cursor)
{
    /// <summary>The earlier end.</summary>
    public TextPosition Start => Anchor.IsAfter(Cursor) ? Cursor : Anchor;

    /// <summary>The later end.</summary>
    public TextPosition End => Anchor.IsAfter(Cursor) ? Anchor : Cursor;

    /// <summary>The characters of line <paramref name="line"/> the selection covers, null when it does not touch the line or the line is empty.</summary>
    /// <param name="line">The line.</param>
    /// <param name="text">The text of the line.</param>
    public ColumnSpan? SpanOn(int line, string text)
    {
        var start = Start;
        var end = End;
        if (line < start.Line || line > end.Line || text.Length == 0)
        {
            return null;
        }

        var last = TextMotions.LineEnd(text);
        var first = line == start.Line ? Math.Min(start.Column, last) : 0;
        var through = line == end.Line ? Math.Min(end.Column, last) : last;

        return through < first ? null : new ColumnSpan(first, through);
    }

    /// <summary>The selected text: the lines joined with a line feed, no trailing one; skipped lines are left out.</summary>
    /// <param name="lines">The lines.</param>
    public string TextIn(ITextLines lines)
    {
        var builder = new StringBuilder();
        var first = Math.Max(Start.Line, 0);
        var last = Math.Min(End.Line, lines.Count - 1);
        var isFirst = true;
        for (var line = first; line <= last; line++)
        {
            if (lines.IsSkipped(line))
            {
                continue;
            }

            if (!isFirst)
            {
                builder.Append('\n');
            }

            isFirst = false;
            var text = lines.TextOf(line);
            if (SpanOn(line, text) is { } span)
            {
                var length = Rune.GetRuneAt(text, span.Last).Utf16SequenceLength;
                builder.Append(text, span.First, span.Last + length - span.First);
            }
        }

        return builder.ToString();
    }
}
