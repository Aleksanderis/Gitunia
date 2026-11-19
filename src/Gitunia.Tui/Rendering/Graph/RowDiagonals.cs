namespace Gitunia.Tui.Rendering.Graph;

/// <summary>What a graph row draws as diagonal lines, decided from the rows next to it (see <see cref="GraphDiagonals"/>).</summary>
/// <param name="SkipLanes">The lanes whose lines starting at the node are not bent out of it here, because the diagonal in the row below replaces the bend.</param>
/// <param name="Marks">The diagonals this row draws for a node in the row above or below.</param>
/// <param name="SkipArrivingLanes">The lanes whose lines ending at the node are not bent into it here, because the diagonal in the row above replaces the bend.</param>
public sealed record RowDiagonals(
    IReadOnlyList<int> SkipLanes,
    IReadOnlyList<DiagonalMark> Marks,
    IReadOnlyList<int> SkipArrivingLanes)
{
    /// <summary>A row without diagonals.</summary>
    public static RowDiagonals None { get; } = new([], [], []);
}
