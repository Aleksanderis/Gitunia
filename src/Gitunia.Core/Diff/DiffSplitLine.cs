namespace Gitunia.Core.Diff;

/// <summary>One line of the side-by-side diff: the row shown on the old (left) side and the one on the new (right) side.</summary>
/// <param name="LeftRow">Row index of the old side, -1 when that side is empty (a full line, and an unchanged line, use the same row on both sides).</param>
/// <param name="RightRow">Row index of the new side, -1 when that side is empty.</param>
/// <param name="IsFull">The line is one row across both sides (a file or hunk header, a note, a warning).</param>
public sealed record DiffSplitLine(int LeftRow, int RightRow, bool IsFull)
{
    /// <summary>The first row of the line in document order.</summary>
    public int FirstRow => LeftRow >= 0 ? LeftRow : RightRow;

    /// <summary>The last row of the line in document order.</summary>
    public int LastRow => Math.Max(LeftRow, RightRow);
}
