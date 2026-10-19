using System.Text;

namespace Gitunia.Core.Text;

/// <summary>
/// Measures text one character at a time while joining zero-width-joiner sequences (family and profession emoji):
/// the terminal draws the whole sequence in two columns (#547, checked against Terminal.Gui). A joiner only joins
/// after an emoji, which is a wide character or a narrow one followed by the emoji variation selector (a narrow one
/// then takes two columns); any other joiner is zero width and joins nothing. Use one counter per text and feed it
/// every character in order.
/// </summary>
public struct WidthCounter
{
    private const int ZeroWidthJoiner = 0x200D;
    private const int EmojiVariationSelector = 0xFE0F;
    private const int WideColumns = 2;

    private int _baseColumns;
    private bool _emojiSelector;
    private bool _afterJoiner;

    /// <summary>Columns the next character adds.</summary>
    /// <param name="rune">The next character of the text.</param>
    public int Next(Rune rune)
    {
        if (rune.Value == ZeroWidthJoiner)
        {
            _afterJoiner = IsEmojiBase();
            return 0;
        }

        if (rune.Value == EmojiVariationSelector)
        {
            _emojiSelector = _baseColumns > 0;
            return 0;
        }

        var width = DisplayWidth.Of(rune);
        if (_afterJoiner)
        {
            _afterJoiner = false;
            return JoinedColumns();
        }

        if (width > 0)
        {
            _baseColumns = width;
            _emojiSelector = false;
        }

        return width;
    }

    private readonly bool IsEmojiBase() => _baseColumns == WideColumns || (_baseColumns > 0 && _emojiSelector);

    // The sequence is two columns wide in total; a narrow emoji base still owes its second column.
    private int JoinedColumns()
    {
        var owed = WideColumns - _baseColumns;
        _baseColumns = WideColumns;
        return owed;
    }
}
