namespace Gitunia.Tui.Rendering.Graph;

/// <summary>What a graph row draws as diagonal lines, decided from the row above it (see <see cref="GraphDiagonals"/>).</summary>
/// <param name="SkipLane">The lane whose line starting at the node is not bent out of it here, because the diagonal in the row below replaces the bend; <see cref="NoLane"/> for none.</param>
/// <param name="Marks">The diagonals this row draws for a node in the row above.</param>
public sealed record RowDiagonals(int SkipLane, IReadOnlyList<DiagonalMark> Marks)
{
    /// <summary>The value of <see cref="SkipLane"/> when no bend is left out.</summary>
    public const int NoLane = -1;

    /// <summary>A row without diagonals.</summary>
    public static RowDiagonals None { get; } = new(NoLane, []);
}
