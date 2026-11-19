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
/// Where the row below does not fit (none loaded, another line in the way, a labelled commit that needs its dot) the
/// order turns round: the line leaving towards the parent keeps its bend and the arriving line becomes a diagonal in the
/// row above, where the old lane ends:
/// <code>
/// │ │
/// │╱       the right lane bends in
/// ○─╮      and a new lane starts at the node
/// │ ●
/// </code>
/// Where neither row fits both bends stay. When another line from the node runs on past the arriving corner (a farther
/// lane merging in, <c>○─┴─╯</c>), the corner would read as joining that line, so the arriving line is a diagonal in
/// the row above as well, next to the departing one below:
/// <code>
/// │╱│
/// ○───╯    the far lane merges in, the near one ends
/// │╲       and starts again
/// </code>
/// Every lane that ends and starts again at the node is planned this way on its own, so with three or more lanes meeting
/// (an octopus merge into freed lanes) only the outermost one on each side keeps its arriving corner, where the run
/// ends, and the lanes it runs over are diagonals above and below.
/// </summary>
public static class GraphDiagonals
{
    /// <summary>The diagonals of one row.</summary>
    /// <param name="graph">The loaded rows.</param>
    /// <param name="index">The row.</param>
    /// <param name="laneColumns">Lanes shown; lanes further right are drawn in the last column, where no diagonal fits.</param>
    public static RowDiagonals PlanFor(CommitGraph graph, int index, int laneColumns)
    {
        List<int>? skipLanes = null;
        List<int>? skipArrivingLanes = null;
        List<DiagonalMark>? marks = null;
        foreach (var junction in JunctionsAt(graph, index, laneColumns))
        {
            if (Departs(graph, index, junction))
            {
                (skipLanes ??= []).Add(junction.OtherLane);
            }

            if (Arrives(graph, index, junction, laneColumns))
            {
                (skipArrivingLanes ??= []).Add(junction.OtherLane);
            }
        }

        foreach (var junction in JunctionsAt(graph, index - 1, laneColumns))
        {
            if (Departs(graph, index - 1, junction))
            {
                (marks ??= []).Add(junction.Below);
            }
        }

        foreach (var junction in JunctionsAt(graph, index + 1, laneColumns))
        {
            if (Arrives(graph, index + 1, junction, laneColumns))
            {
                (marks ??= []).Add(junction.Above);
            }
        }

        if (marks is null && skipLanes is null && skipArrivingLanes is null)
        {
            return RowDiagonals.None;
        }

        return new RowDiagonals(skipLanes ?? [], marks ?? [], skipArrivingLanes ?? []);
    }

    /// <summary>
    /// The shape of a row on its own: each other lane that ends at the node and where a new lane starts again. Other
    /// merges into the node do not matter. Lanes outside <paramref name="laneColumns"/> are left out.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="laneColumns">Lanes shown.</param>
    public static IReadOnlyList<DiagonalJunction> JunctionsOf(GraphRow row, int laneColumns)
    {
        List<DiagonalJunction>? found = null;
        foreach (var ending in row.Edges)
        {
            var isOther = ending.Kind == GraphEdgeKind.Incoming && ending.FromLane != row.Lane;
            if (!isOther || Math.Max(ending.FromLane, row.Lane) >= laneColumns)
            {
                continue;
            }

            foreach (var starting in row.Edges)
            {
                if (starting.Kind == GraphEdgeKind.Outgoing && starting.ToLane == ending.FromLane)
                {
                    found ??= [];
                    found.Add(new DiagonalJunction(row.Lane, ending.FromLane, starting.Color, ending.Color));
                }
            }
        }

        return found ?? [];
    }

    private static IReadOnlyList<DiagonalJunction> JunctionsAt(CommitGraph graph, int index, int laneColumns)
    {
        if (index < 0 || index >= graph.Count)
        {
            return [];
        }

        return JunctionsOf(graph[index].Row, laneColumns);
    }

    // The departing line of a junction is a diagonal: the row below exists and has room for it.
    private static bool Departs(CommitGraph graph, int index, DiagonalJunction junction) =>
        index < graph.Count - 1 && HasRoom(graph[index + 1], junction.OtherLane);

    // The arriving line of a junction is a diagonal: the row below has no room for the departing diagonal or another
    // line runs over the arriving corner; the row above exists, has room for it, and no departing diagonal from further
    // up takes the cell. The last loaded row keeps its bends: the row below is not loaded yet and may take the
    // departing diagonal later.
    private static bool Arrives(CommitGraph graph, int index, DiagonalJunction junction, int laneColumns)
    {
        if (index < 1 || index >= graph.Count - 1)
        {
            return false;
        }

        var departs = Departs(graph, index, junction);
        if ((departs && !IsCornerCrossed(graph[index].Row, junction)) || !HasRoom(graph[index - 1], junction.OtherLane))
        {
            return false;
        }

        foreach (var taken in JunctionsAt(graph, index - 2, laneColumns))
        {
            if (taken.OtherLane == junction.OtherLane && Departs(graph, index - 2, taken))
            {
                return false;
            }
        }

        return true;
    }

    // Another line between the node and a lane farther out on the junction's side runs over the arriving corner's cell.
    private static bool IsCornerCrossed(GraphRow row, DiagonalJunction junction)
    {
        var isRight = junction.OtherLane > junction.NodeLane;
        foreach (var edge in row.Edges)
        {
            if (edge.Kind == GraphEdgeKind.Through)
            {
                continue;
            }

            var farLane = edge.Kind == GraphEdgeKind.Incoming ? edge.FromLane : edge.ToLane;
            if (isRight ? farLane > junction.OtherLane : farLane < junction.OtherLane)
            {
                return true;
            }
        }

        return false;
    }

    // The cell of the lane in the row next to the node holds only a line running through it or a plain commit of that
    // lane (no ref: a label needs its dot), and no bend runs over it.
    private static bool HasRoom(GraphEntry entry, int lane)
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
