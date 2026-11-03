namespace Gitunia.Core.Editing;

/// <summary>The selected part of a <see cref="MessageEditor"/>: never empty, <see cref="Start"/> before <see cref="End"/>.</summary>
/// <param name="Start">The first selected position.</param>
/// <param name="End">The position after the last selected element.</param>
public readonly record struct TextSelection(TextPosition Start, TextPosition End)
{
    /// <summary>
    /// The selected UTF-16 range of one line, from the line start or the selection start to the line end or the
    /// selection end; null when the selection does not touch the line. The range is empty on a line that is only
    /// selected through its line break.
    /// </summary>
    /// <param name="row">Zero-based line.</param>
    /// <param name="lineLength">Length of the line.</param>
    public ColumnRange? InLine(int row, int lineLength)
    {
        if (row < Start.Row || row > End.Row)
        {
            return null;
        }

        var start = row == Start.Row ? Start.Column : 0;
        var end = row == End.Row ? End.Column : lineLength;

        return new ColumnRange(start, end);
    }

    /// <summary>True when the line break after the line is selected too.</summary>
    /// <param name="row">Zero-based line.</param>
    public bool IncludesLineBreak(int row) => row >= Start.Row && row < End.Row;
}
