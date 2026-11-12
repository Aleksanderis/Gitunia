using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Turns the edges of a <see cref="GraphRow"/> into characters: two columns per lane, the second one carries
/// connecting lines. Each cell collects the directions its lines leave through and the glyph set maps the
/// combination to a character, so overlapping lines (forks and joins) always draw correctly. A connecting line that
/// runs over another lane is not drawn as a crossing: it stays one straight line and the lane has a gap there.
/// </summary>
public static class LaneCellBuilder
{
    /// <param name="row">The layout of the row.</param>
    /// <param name="laneColumns">Lanes shown; lanes further right are drawn in the last column.</param>
    /// <param name="glyphs">The characters to use.</param>
    /// <param name="isDecorated">The commit carries a ref (branch, tag, HEAD, remote); it keeps a node dot even in the middle of a line (#280).</param>
    /// <param name="diagonals">The diagonal lines of the row (see <see cref="GraphDiagonals"/>); null for none.</param>
    public static GraphCell[] Build(
        GraphRow row,
        int laneColumns,
        GraphGlyphSet glyphs,
        bool isDecorated,
        RowDiagonals? diagonals = null)
    {
        var plan = diagonals ?? RowDiagonals.None;
        var directions = new LineDirections[laneColumns * 2];
        var colors = new int[laneColumns * 2];
        Array.Fill(colors, -1);

        var nodeLane = Math.Min(row.Lane, laneColumns - 1);
        var outgoingCount = 0;
        var incomingCount = 0;
        var isOnLane = true;
        foreach (var edge in row.Edges)
        {
            switch (edge.Kind)
            {
                case GraphEdgeKind.Through:
                    break;
                case GraphEdgeKind.Incoming:
                    incomingCount++;
                    isOnLane &= edge.FromLane == row.Lane;
                    var incomingLane = Math.Min(edge.FromLane, laneColumns - 1);
                    Connect(directions, colors, incomingLane, nodeLane, LineDirections.Up, edge.Color);
                    break;
                default:
                    outgoingCount++;
                    isOnLane &= edge.ToLane == row.Lane;
                    var outgoingLane = Math.Min(edge.ToLane, laneColumns - 1);
                    if (edge.ToLane != plan.SkipLane)
                    {
                        Connect(directions, colors, outgoingLane, nodeLane, LineDirections.Down, edge.Color);
                    }

                    break;
            }
        }

        AddThroughLanes(row, laneColumns, directions, colors);

        var cells = new GraphCell[laneColumns * 2];
        for (var column = 0; column < cells.Length; column++)
        {
            cells[column] = new GraphCell(glyphs.ForLane(directions[column]).ToString(), colors[column]);
            if (directions[column] == LineDirections.None)
            {
                cells[column] = new GraphCell(" ", -1);
            }
        }

        // An unlabelled commit with exactly one child and one parent, both on its lane, is part of a straight line, not a node.
        var isLineCommit = !isDecorated && isOnLane && incomingCount == 1 && outgoingCount == 1;
        var nodeGlyph = outgoingCount > 1 ? glyphs.MergeNode : glyphs.Node;
        if (isLineCommit)
        {
            nodeGlyph = glyphs.LineNode;
        }

        cells[nodeLane * 2] = new GraphCell(nodeGlyph.ToString(), row.NodeColor);

        // A diagonal takes the cell of its lane: the line running through it, or a plain commit of that lane (it is the diagonal itself).
        foreach (var mark in plan.Marks.Where(mark => mark.Column < cells.Length))
        {
            cells[mark.Column] = new GraphCell(mark.IsToRight ? glyphs.DepartRight : glyphs.DepartLeft, mark.Color);
        }

        return cells;
    }

    // A lane that passes the row is left out where a connecting line only runs over it (a line that ends there is a junction): the line stays straight, the lane has a gap.
    private static void AddThroughLanes(GraphRow row, int laneColumns, LineDirections[] directions, int[] colors)
    {
        foreach (var edge in row.Edges)
        {
            if (edge.Kind != GraphEdgeKind.Through)
            {
                continue;
            }

            var column = Math.Min(edge.FromLane, laneColumns - 1) * 2;
            if (directions[column] != (LineDirections.Left | LineDirections.Right))
            {
                Add(directions, colors, column, LineDirections.Up | LineDirections.Down, edge.Color);
            }
        }
    }

    // A line between the node and another lane: horizontal in between, a corner (up or down plus sideways) at the other lane.
    private static void Connect(
        LineDirections[] directions,
        int[] colors,
        int otherLane,
        int nodeLane,
        LineDirections vertical,
        int color)
    {
        if (otherLane == nodeLane)
        {
            return;
        }

        var from = Math.Min(otherLane, nodeLane);
        var to = Math.Max(otherLane, nodeLane);
        var isRight = otherLane > nodeLane;
        for (var column = (from * 2) + 1; column < to * 2; column++)
        {
            Add(directions, colors, column, LineDirections.Left | LineDirections.Right, color);
        }

        var sideways = isRight ? LineDirections.Left : LineDirections.Right;
        Add(directions, colors, otherLane * 2, vertical | sideways, color);
    }

    // The first line in a cell decides its colour.
    private static void Add(LineDirections[] directions, int[] colors, int column, LineDirections added, int color)
    {
        if (directions[column] == LineDirections.None)
        {
            colors[column] = color;
        }

        directions[column] |= added;
    }
}
