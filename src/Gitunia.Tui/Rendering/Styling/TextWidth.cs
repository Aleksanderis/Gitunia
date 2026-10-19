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
}
