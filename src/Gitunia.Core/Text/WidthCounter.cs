using System.Text;

namespace Gitunia.Core.Text;

/// <summary>
/// Measures text one character at a time while joining zero-width-joiner sequences (family and profession emoji):
/// the terminal draws the whole sequence in two columns (#547, checked against Terminal.Gui). A joiner only joins an
/// emoji to an emoji; any other joiner is zero width and joins nothing. A narrow emoji base then takes two columns,
/// so a caller that cuts text between characters can leave a joiner at the end of a piece for such a sequence. Use one
/// counter per text, never copy it mid-loop, and feed it every character in order.
/// </summary>
public struct WidthCounter
{
    private const int ZeroWidthJoiner = 0x200D;
    private const int WideColumns = 2;

    // Approximate Extended_Pictographic: the emoji blocks, the arrows and symbols blocks that hold the narrow ones.
    private const int FirstTechnical = 0x2300;
    private const int LastTechnical = 0x23FF;
    private const int FirstSymbol = 0x2600;
    private const int LastDingbat = 0x27BF;
    private const int FirstSymbolsAndArrows = 0x2B00;
    private const int LastSymbolsAndArrows = 0x2BFF;
    private const int FirstEmojiPlane = 0x1F000;
    private const int LastEmojiPlane = 0x1FAFF;

    private int _baseColumns;
    private bool _basePictographic;
    private bool _afterJoiner;

    /// <summary>Columns the next character adds.</summary>
    /// <param name="rune">The next character of the text.</param>
    public int Next(Rune rune)
    {
        if (rune.Value == ZeroWidthJoiner)
        {
            _afterJoiner = _basePictographic && !_afterJoiner;
            _basePictographic &= _afterJoiner;
            return 0;
        }

        var width = DisplayWidth.Of(rune);
        var pictographic = IsPictographic(rune.Value);
        if (_afterJoiner)
        {
            _afterJoiner = false;
            if (pictographic)
            {
                return JoinedColumns();
            }
        }

        if (width > 0)
        {
            _baseColumns = width;
            _basePictographic = pictographic;
        }

        return width;
    }

    private static bool IsPictographic(int value)
    {
        return value is >= FirstEmojiPlane and <= LastEmojiPlane
            or >= FirstTechnical and <= LastTechnical
            or >= FirstSymbol and <= LastDingbat
            or >= FirstSymbolsAndArrows and <= LastSymbolsAndArrows;
    }

    // The sequence is two columns wide in total; a narrow emoji base still owes its second column.
    private int JoinedColumns()
    {
        var owed = WideColumns - _baseColumns;
        _baseColumns = WideColumns;
        return owed;
    }
}
