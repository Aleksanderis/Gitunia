using System.Globalization;
using System.Text;

namespace Gitunia.Tui.Rendering;

/// <summary>Terminal column widths of text: wide (CJK, emoji) characters take two columns, combining marks none.</summary>
public static class TextWidth
{
    /// <summary>Columns <paramref name="text"/> occupies.</summary>
    public static int Of(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += Of(rune);
        }

        return width;
    }

    /// <summary>Columns one character occupies. Control characters count as one: they are shown as a space.</summary>
    public static int Of(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    /// <summary>
    /// Makes <paramref name="text"/> fit <paramref name="columns"/>: control characters become spaces and an overlong text
    /// is cut and ends with <paramref name="ellipsis"/>. A wide character is never split.
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
        foreach (var rune in text.EnumerateRunes())
        {
            var runeWidth = Of(rune);
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

    private static bool IsWide(int value) => value switch
    {
        >= 0x1100 and <= 0x115F => true,
        >= 0x2E80 and <= 0x303E => true,
        >= 0x3041 and <= 0xA4CF => true,
        >= 0xAC00 and <= 0xD7A3 => true,
        >= 0xF900 and <= 0xFAFF => true,
        >= 0xFE30 and <= 0xFE6F => true,
        >= 0xFF00 and <= 0xFF60 => true,
        >= 0xFFE0 and <= 0xFFE6 => true,
        >= 0x1F300 and <= 0x1F64F => true,
        >= 0x1F900 and <= 0x1F9FF => true,
        >= 0x20000 and <= 0x3FFFD => true,
        _ => false,
    };
}
