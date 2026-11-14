namespace Gitunia.Tui.Rendering.Graph;

/// <summary>A commit where another lane ends at the node and starts again below it (<see cref="GraphDiagonals"/>).</summary>
/// <param name="NodeLane">The lane of the node.</param>
/// <param name="OtherLane">The other lane that ends at the node and starts again below it.</param>
/// <param name="OutgoingColor">Colour slot of the line that starts at the node.</param>
public readonly record struct DiagonalJunction(int NodeLane, int OtherLane, int OutgoingColor)
{
    /// <summary>The left one of the two lanes.</summary>
    public int LeftLane => Math.Min(NodeLane, OtherLane);

    /// <summary>The diagonal in the row below the node, in the cell of the new lane: the starting line bends out.</summary>
    public DiagonalMark Below => new(OtherLane, OtherLane > NodeLane, OutgoingColor);
}
