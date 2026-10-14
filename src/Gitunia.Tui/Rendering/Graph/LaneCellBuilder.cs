using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Turns the edges of a <see cref="GraphRow"/> into characters: two columns per lane, the second one carries
/// connecting lines. Each cell collects the directions its lines leave through and the glyph set maps the
/// combination to a character, so overlapping lines (forks, joins, crossings) always draw correctly.
/// </summary>
public static class LaneCellBuilder
{
    /// <param name="row">The layout of the row.</param>
    /// <param name="laneColumns">Lanes shown; lanes further right are drawn in the last column.</param>
    /// <param name="glyphs">The characters to use.</param>
    /// <param name="isDecorated">The commit carries a ref (branch, tag, HEAD, remote); it keeps a node dot even in the middle of a line (#280).</param>
    public static GraphCell[] Build(GraphRow row, int laneColumns, GraphGlyphSet glyphs, bool isDecorated)
    {
        var directions = new LineDirections[laneColumns * 2];
        var lanes = new int[laneColumns * 2];
        Array.Fill(lanes, -1);

        var nodeLane = Math.Min(row.Lane, laneColumns - 1);
        var outgoingCount = 0;
        var incomingCount = 0;
        var isOnLane = true;
        foreach (var edge in row.Edges)
        {
            switch (edge.Kind)
            {
                case GraphEdgeKind.Through:
                    var throughLane = Math.Min(edge.FromLane, laneColumns - 1);
                    Add(directions, lanes, throughLane * 2, LineDirections.Up | LineDirections.Down, throughLane);
                    break;
                case GraphEdgeKind.Incoming:
                    incomingCount++;
                    isOnLane &= edge.FromLane == row.Lane;
                    Connect(directions, lanes, Math.Min(edge.FromLane, laneColumns - 1), nodeLane, LineDirections.Up);
                    break;
                default:
                    outgoingCount++;
                    isOnLane &= edge.ToLane == row.Lane;
                    Connect(directions, lanes, Math.Min(edge.ToLane, laneColumns - 1), nodeLane, LineDirections.Down);
                    break;
            }
        }

        var cells = new GraphCell[laneColumns * 2];
        for (var column = 0; column < cells.Length; column++)
        {
            cells[column] = directions[column] == LineDirections.None
                ? new GraphCell(' ', -1)
                : new GraphCell(glyphs.For(directions[column]), lanes[column]);
        }

        // A plain commit with exactly one child and one parent, both on its lane, is part of a straight line, not a node.
        var isLineCommit = !isDecorated && isOnLane && incomingCount == 1 && outgoingCount == 1;
        var nodeGlyph = isLineCommit ? glyphs.LineNode : outgoingCount > 1 ? glyphs.MergeNode : glyphs.Node;
        cells[nodeLane * 2] = new GraphCell(nodeGlyph, nodeLane);
        return cells;
    }

    // A line between the node and another lane: horizontal in between, a corner (up or down plus sideways) at the other lane.
    private static void Connect(LineDirections[] directions, int[] lanes, int otherLane, int nodeLane, LineDirections vertical)
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
            Add(directions, lanes, column, LineDirections.Left | LineDirections.Right, otherLane);
        }

        Add(directions, lanes, otherLane * 2, vertical | (isRight ? LineDirections.Left : LineDirections.Right), otherLane);
    }

    // The first line in a cell decides its colour (a through lane keeps its own where a connection crosses it).
    private static void Add(LineDirections[] directions, int[] lanes, int column, LineDirections added, int lane)
    {
        if (directions[column] == LineDirections.None)
        {
            lanes[column] = lane;
        }

        directions[column] |= added;
    }
}
