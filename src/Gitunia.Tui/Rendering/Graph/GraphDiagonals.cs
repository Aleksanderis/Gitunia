using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Decides where a graph draws a diagonal line instead of a bend. It is for one case: a commit where a lane ends and
/// another lane starts in the same lane, next to the node or further away (the arriving bend then ends a horizontal
/// run, <c>○───╯</c>, and the diagonal is still in the cell right below its corner). A line that only arrives or only
/// leaves keeps its rounded corner (<c>╮ ╯ ╭ ╰</c>): it is unambiguous. Drawn with two bends both would share one
/// character that reads as a lane running straight through (<c>├</c>, <c>┤</c>, <c>┼</c>). The line arriving from
/// above keeps its bend, which shows the lane ends here; the line leaving towards the parent becomes a diagonal in the
/// row below, where the new lane starts:
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
    /// The shape of a row on its own: another lane ends at the node and a new lane starts in the same lane. Other
    /// merges into the node do not matter. When several lanes end and start again (an octopus merge into freed lanes)
    /// the one nearest to the node gets the diagonal and the others keep their bends; two equally near ones (one on
    /// each side) are ambiguous and all keep their bends.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="laneColumns">Lanes shown.</param>
    public static DiagonalJunction? JunctionOf(GraphRow row, int laneColumns)
    {
        DiagonalJunction? found = null;
        var tied = false;
        foreach (var ending in row.Edges)
        {
            var isOther = ending.Kind == GraphEdgeKind.Incoming && ending.FromLane != row.Lane;
            if (!isOther || Math.Max(ending.FromLane, row.Lane) >= laneColumns)
            {
                continue;
            }

            foreach (var starting in row.Edges)
            {
                if (starting.Kind != GraphEdgeKind.Outgoing || starting.ToLane != ending.FromLane)
                {
                    continue;
                }

                var distance = Math.Abs(ending.FromLane - row.Lane);
                var foundDistance = found is { } known ? Math.Abs(known.OtherLane - row.Lane) : int.MaxValue;
                if (distance < foundDistance)
                {
                    found = new DiagonalJunction(row.Lane, ending.FromLane, starting.Color);
                    tied = false;
                }
                else if (distance == foundDistance && found is { } same && same.OtherLane != ending.FromLane)
                {
                    tied = true;
                }
            }
        }

        return tied ? null : found;
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
