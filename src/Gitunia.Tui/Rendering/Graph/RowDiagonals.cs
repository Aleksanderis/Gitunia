namespace Gitunia.Tui.Rendering.Graph;

/// <summary>What a graph row draws as diagonal lines, decided from the rows next to it (see <see cref="GraphDiagonals"/>).</summary>
/// <param name="SkipLane">The lane whose line starting at the node is not bent out of it here, because the diagonal in the row below replaces the bend; <see cref="NoLane"/> for none.</param>
/// <param name="Marks">The diagonals this row draws for a node in the row above or below.</param>
/// <param name="SkipArrivingLane">The lane whose line ending at the node is not bent into it here, because the diagonal in the row above replaces the bend; <see cref="NoLane"/> for none.</param>
public sealed record RowDiagonals(int SkipLane, IReadOnlyList<DiagonalMark> Marks, int SkipArrivingLane = RowDiagonals.NoLane)
{
    /// <summary>The value of <see cref="SkipLane"/> and <see cref="SkipArrivingLane"/> when no bend is left out.</summary>
    public const int NoLane = -1;

    /// <summary>A row without diagonals.</summary>
    public static RowDiagonals None { get; } = new(NoLane, []);
}
