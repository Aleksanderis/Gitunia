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

    /// <summary>The caret drawn after the last character (an empty cell).</summary>
    public const string EndGlyph = "_";

    /// <summary>The caret drawn between two characters, in text a style cannot reach.</summary>
    public const string MiddleGlyph = "|";

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
    /// The text with the caret drawn in, for prompts shown as plain text: <see cref="EndGlyph"/> after the last
    /// character, <see cref="MiddleGlyph"/> inside the text. When the result is wider than
    /// <paramref name="columns"/>, the part around the caret stays: the text before it is cut from the left and the
    /// text after it from the right.
    /// </summary>
    public static string Mark(string text, int caret, int columns = int.MaxValue)
    {
        var at = Clamp(text, caret);
        var glyph = at == text.Length ? EndGlyph : MiddleGlyph;
        var before = text[..at];
        var after = text[at..];
        if (DisplayWidth.Of(text) + glyph.Length <= columns)
        {
            return $"{before}{glyph}{after}";
        }

        var room = Math.Max(0, columns - glyph.Length);
        var afterRoom = Math.Min(DisplayWidth.Of(after), room / 2);
        var kept = Tail(before, room - afterRoom);

        return $"{kept}{glyph}{Head(after, room - DisplayWidth.Of(kept))}";
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
