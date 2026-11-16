using System.Globalization;
using System.Text;
using Gitunia.Core.Text;

namespace Gitunia.Core.Editing;

/// <summary>
/// The caret of a single-line prompt. States store a column and default to <see cref="End"/> ("after the last
/// character"), so a prompt opened with text, or whose text is replaced as a value, starts with the caret at the end
/// without the opener computing it. Every read goes through <see cref="Clamp"/>.
/// </summary>
public static class PromptCaret
{
    /// <summary>The caret after the last character of whatever the text is.</summary>
    public const int End = int.MaxValue;

    /// <summary>The cursor cell at the end of the text: a blank.</summary>
    public const string EndCell = " ";

    /// <summary>The caret limited to the text and moved back to the start of the text element it falls in.</summary>
    public static int Clamp(string text, int caret)
    {
        if (caret >= text.Length)
        {
            return text.Length;
        }

        if (caret <= 0)
        {
            return 0;
        }

        var boundary = 0;
        while (true)
        {
            var next = boundary + StringInfo.GetNextTextElementLength(text.AsSpan(boundary));
            if (next > caret)
            {
                return boundary;
            }

            boundary = next;
        }
    }

    /// <summary>The caret column before the one at <paramref name="caret"/>.</summary>
    public static int Previous(string text, int caret)
    {
        var target = Clamp(text, caret);
        var previous = 0;
        for (var boundary = 0; boundary < target; boundary += StringInfo.GetNextTextElementLength(text.AsSpan(boundary)))
        {
            previous = boundary;
        }

        return previous;
    }

    /// <summary>The caret column after the one at <paramref name="caret"/>.</summary>
    public static int Next(string text, int caret)
    {
        var target = Clamp(text, caret);
        return target >= text.Length ? text.Length : target + StringInfo.GetNextTextElementLength(text.AsSpan(target));
    }

    /// <summary>
    /// The text split around the caret for a field drawn with a cursor cell: the text element under the caret is the
    /// cell (a blank at the end), so the text is not pushed apart. When the text is wider than
    /// <paramref name="columns"/>, the part around the caret stays: before it is cut from the left and after it from
    /// the right. Text elements are never split.
    /// </summary>
    /// <param name="text">The field text.</param>
    /// <param name="caret">The caret column (<see cref="End"/> for after the last character).</param>
    /// <param name="columns">Columns the whole field, cursor cell included, may take.</param>
    public static CaretText Split(string text, int caret, int columns = int.MaxValue)
    {
        var at = Clamp(text, caret);
        var cell = at == text.Length ? EndCell : text[at..(at + StringInfo.GetNextTextElementLength(text.AsSpan(at)))];
        var before = text[..at];
        var after = text[(at + (at == text.Length ? 0 : cell.Length))..];
        var cellWidth = Math.Max(1, DisplayWidth.Of(cell));
        if (DisplayWidth.Of(before) + DisplayWidth.Of(after) + cellWidth <= columns)
        {
            return new CaretText(before, cell, after);
        }

        var room = Math.Max(0, columns - cellWidth);
        var afterRoom = Math.Min(DisplayWidth.Of(after), room / 2);
        var kept = Tail(before, room - afterRoom);

        return new CaretText(kept, cell, Head(after, room - DisplayWidth.Of(kept)));
    }

    private static string Tail(string text, int columns)
    {
        var used = 0;
        var keptFrom = text.Length;
        var starts = StringInfo.ParseCombiningCharacters(text);
        for (var index = starts.Length - 1; index >= 0; index--)
        {
            var end = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            var width = DisplayWidth.Of(text[starts[index]..end]);
            if (used + width > columns)
            {
                break;
            }

            keptFrom = starts[index];
            used += width;
        }

        return text[keptFrom..];
    }

    private static string Head(string text, int columns)
    {
        var builder = new StringBuilder();
        var used = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            used += DisplayWidth.Of(element);
            if (used > columns)
            {
                break;
            }

            builder.Append(element);
        }

        return builder.ToString();
    }
}

/// <summary>A field text split around its caret: <see cref="Cell"/> is drawn as the cursor.</summary>
/// <param name="Before">Text before the cursor cell.</param>
/// <param name="Cell">The character under the caret, or a blank at the end of the text.</param>
/// <param name="After">Text after the cursor cell.</param>
public readonly record struct CaretText(string Before, string Cell, string After);
