using System.Globalization;
using System.Text;

namespace Gitunia.Core.Text;

/// <summary>Terminal column widths of text: wide (CJK, emoji) characters take two, combining marks none.</summary>
public static class DisplayWidth
{
    /// <summary>Columns <paramref name="text"/> occupies.</summary>
    /// <param name="text">The text.</param>
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
    /// <param name="rune">The character.</param>
    public static int Of(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        return IsWide(rune.Value) ? 2 : 1;
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
