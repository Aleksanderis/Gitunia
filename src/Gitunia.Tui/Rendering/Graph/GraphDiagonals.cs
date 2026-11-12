using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Decides where a graph draws a diagonal line instead of a bend. It is for one case: a commit where a lane ends and
/// another lane starts in the same lane, one lane away. Drawn with two bends both would share one character that reads
/// as a lane running straight through (<c>├</c>, <c>┤</c>, <c>┼</c>). The line arriving from above keeps its bend, which shows the
/// lane ends here; the line leaving towards the parent becomes a diagonal in the row below, where the new lane starts:
/// <code>
/// │ │
/// ○─╯      the right lane ends at the node
/// │╲       and a new lane bends out
/// │ │
/// </code>
/// Where the row below does not fit (none loaded, another line in the way) the bend stays.
/// </summary>
public static class GraphDiagonals
{
    /// <summary>The diagonals of one row.</summary>
    /// <param name="graph">The loaded rows.</param>
    /// <param name="index">The row.</param>
    /// <param name="laneColumns">Lanes shown; lanes further right are drawn in the last column, where no diagonal fits.</param>
    public static RowDiagonals PlanFor(CommitGraph graph, int index, int laneColumns)
    {
        var skipLane = DepartureAt(graph, index, laneColumns) is { } own ? own.OtherLane : RowDiagonals.NoLane;
        List<DiagonalMark> marks = [];
        if (DepartureAt(graph, index - 1, laneColumns) is { } above)
        {
            marks.Add(above.Below);
        }

        return marks.Count == 0 && skipLane == RowDiagonals.NoLane ? RowDiagonals.None : new RowDiagonals(skipLane, marks);
    }

    /// <summary>
    /// The shape of a row on its own: a lane one away from the node ends at it and a new lane starts in the same lane.
    /// Other merges into the node do not matter; a lane ending and starting on both sides is
    /// ambiguous and keeps its bends.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="laneColumns">Lanes shown.</param>
    public static DiagonalJunction? JunctionOf(GraphRow row, int laneColumns)
    {
        DiagonalJunction? found = null;
        foreach (var ending in row.Edges)
        {
            var isNext = ending.Kind == GraphEdgeKind.Incoming && Math.Abs(ending.FromLane - row.Lane) == 1;
            if (!isNext || Math.Max(ending.FromLane, row.Lane) >= laneColumns)
            {
                continue;
            }

            foreach (var starting in row.Edges)
            {
                if (starting.Kind != GraphEdgeKind.Outgoing || starting.ToLane != ending.FromLane)
                {
                    continue;
                }

                if (found is not null)
                {
                    return null;
                }

                found = new DiagonalJunction(row.Lane, ending.FromLane, starting.Color);
            }
        }

        return found;
    }

    // The junction of a row when its departing line is drawn as a diagonal: the row below exists and has room for it.
    private static DiagonalJunction? DepartureAt(CommitGraph graph, int index, int laneColumns)
    {
        if (index < 0 || index >= graph.Count - 1 || JunctionOf(graph[index].Row, laneColumns) is not { } junction)
        {
            return null;
        }

        return HasRoomBelow(graph[index + 1], junction.OtherLane) ? junction : null;
    }

    // The cell of the new lane in the row below holds only a line running through it or a plain commit of that lane (no
    // ref: a label needs its dot), and no bend runs over it.
    private static bool HasRoomBelow(GraphEntry entry, int lane)
    {
        var row = entry.Row;
        var hasLane = false;
        var endsHere = 0;
        var startsHere = 0;
        foreach (var edge in row.Edges)
        {
            if (edge.Kind == GraphEdgeKind.Through)
            {
                hasLane |= edge.FromLane == lane;
                continue;
            }

            var farLane = edge.Kind == GraphEdgeKind.Incoming ? edge.FromLane : edge.ToLane;
            if (farLane == row.Lane)
            {
                continue;
            }

            if (Math.Min(farLane, row.Lane) <= lane && lane <= Math.Max(farLane, row.Lane))
            {
                return false;
            }
        }

        if (row.Lane != lane)
        {
            return hasLane;
        }

        foreach (var edge in row.Edges)
        {
            endsHere += edge.Kind == GraphEdgeKind.Incoming ? 1 : 0;
            startsHere += edge.Kind == GraphEdgeKind.Outgoing ? 1 : 0;
        }

        return entry.Decorations.Count == 0 && endsHere == 1 && startsHere == 1;
    }
}
