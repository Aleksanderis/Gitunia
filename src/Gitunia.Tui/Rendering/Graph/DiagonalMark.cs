namespace Gitunia.Tui.Rendering.Graph;

/// <summary>A diagonal line in the lane cell of a row: where a lane starts at the node of the row above and bends out of it.</summary>
/// <param name="Lane">The lane that starts; its cell in the row holds the diagonal in place of the line running through it.</param>
/// <param name="IsToRight">The lane lies to the right of the node, so the line falls to the right, else to the left.</param>
/// <param name="Color">Colour slot of the line.</param>
public readonly record struct DiagonalMark(int Lane, bool IsToRight, int Color)
{
    /// <summary>The column of the diagonal in the lane area: the cell of its lane.</summary>
    public int Column => Lane * 2;
}
