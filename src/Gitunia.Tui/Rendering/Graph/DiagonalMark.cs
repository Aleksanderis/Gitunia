namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// A diagonal line in the lane cell of a row: where a lane starts at the node of the row above and bends out of it, or
/// ends at the node of the row below and bends into it.
/// </summary>
/// <param name="Lane">The lane; its cell in the row holds the diagonal in place of the line running through it.</param>
/// <param name="IsToRight">The line falls to the right (<c>╲</c>), else to the left (<c>╱</c>).</param>
/// <param name="Color">Colour slot of the line.</param>
public readonly record struct DiagonalMark(int Lane, bool IsToRight, int Color)
{
    /// <summary>The column of the diagonal in the lane area: the cell of its lane.</summary>
    public int Column => Lane * 2;
}
