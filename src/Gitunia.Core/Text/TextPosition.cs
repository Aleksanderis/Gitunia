namespace Gitunia.Core.Text;

/// <summary>A place in the text lines of a read-only pane: a line and the UTF-16 index of a character in it.</summary>
/// <param name="Line">The line (the row of the pane's list).</param>
/// <param name="Column">The index of the first UTF-16 unit of the character in the line's text.</param>
public readonly record struct TextPosition(int Line, int Column)
{
    /// <summary>This position is after <paramref name="other"/> (a later line, or the same line and a later column).</summary>
    /// <param name="other">The position to compare with.</param>
    public bool IsAfter(TextPosition other) => Line != other.Line ? Line > other.Line : Column > other.Column;
}
