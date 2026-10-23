using System.Text;
using Gitunia.Core.Text;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Terminal column widths of text: wide (CJK, emoji) characters take two columns, combining marks none.</summary>
public static class TextWidth
{
    /// <summary>Columns <paramref name="text"/> occupies.</summary>
    public static int Of(string text) => DisplayWidth.Of(text);

    /// <summary>Columns one character occupies. Control characters count as one: they are shown as a space.</summary>
    public static int Of(Rune rune) => DisplayWidth.Of(rune);

    /// <summary>
    /// Makes <paramref name="text"/> fit <paramref name="columns"/>: control characters become spaces and an overlong text
    /// is cut and ends with <paramref name="ellipsis"/>. A wide character is never split. Joined emoji sequences are
    /// measured by <see cref="WidthCounter"/>.
    /// </summary>
    public static string Fit(string text, int columns, string ellipsis)
    {
        if (columns <= 0)
        {
            return string.Empty;
        }

        var fits = Of(text) <= columns;
        var limit = fits ? columns : Math.Max(0, columns - Of(ellipsis));
        var builder = new StringBuilder(Math.Min(text.Length, columns * 2));
        var width = 0;
        var counter = new WidthCounter();
        foreach (var rune in text.EnumerateRunes())
        {
            var runeWidth = counter.Next(rune);
            if (width + runeWidth > limit)
            {
                break;
            }

            builder.Append(Rune.IsControl(rune) ? " " : rune.ToString());
            width += runeWidth;
        }

        if (!fits)
        {
            builder.Append(columns >= Of(ellipsis) ? ellipsis : string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>
    /// The end of <paramref name="text"/> that fits <paramref name="columns"/>, led by <paramref name="ellipsis"/>
    /// when something
    /// was cut off the start. A wide character is never split and the cut never leaves a lone combining mark at the
    /// start.
    /// </summary>
    public static string FitStart(string text, int columns, string ellipsis)
    {
        if (columns <= 0)
        {
            return string.Empty;
        }

        if (Of(text) <= columns)
        {
            return text;
        }

        var limit = Math.Max(0, columns - Of(ellipsis));
        var kept = new List<Rune>();
        var width = 0;
        foreach (var rune in text.EnumerateRunes().Reverse())
        {
            var runeWidth = Of(rune);
            if (width + runeWidth > limit)
            {
                break;
            }

            kept.Add(rune);
            width += runeWidth;
        }

        kept.Reverse();
        var start = kept.FindIndex(rune => Of(rune) > 0);
        var tail = start < 0 ? string.Empty : string.Concat(kept.Skip(start));

        return columns >= Of(ellipsis) ? $"{ellipsis}{tail}" : tail;
    }

    /// <summary>
    /// Makes <paramref name="text"/> fit <paramref name="columns"/> by cutting the middle (<c>abc…xyz</c>), so the start
    /// and the distinguishing tail stay visible. Short texts come back as <see cref="Fit"/> returns them.
    /// </summary>
    public static string FitMiddle(string text, int columns, string ellipsis)
    {
        var ellipsisWidth = Of(ellipsis);
        if (Of(text) <= columns || columns <= ellipsisWidth + 2)
        {
            return Fit(text, columns, ellipsis);
        }

        var room = columns - ellipsisWidth;
        var tailColumns = room / 2;
        var headColumns = room - tailColumns;

        return $"{Fit(text, headColumns, string.Empty)}{ellipsis}{FitStart(text, tailColumns, string.Empty)}";
    }
}
