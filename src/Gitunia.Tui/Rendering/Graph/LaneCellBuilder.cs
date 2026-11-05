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
                    var throughLane = Math.Min(edge.FromLane, laneColumns - 1);
                    var shiftedLane = Math.Min(edge.ToLane, laneColumns - 1);
                    if (shiftedLane == throughLane)
                    {
                        Add(directions, colors, throughLane * 2, LineDirections.Up | LineDirections.Down, edge.Color);
                        break;
                    }

                    Add(directions, colors, throughLane * 2, LineDirections.Up | LineDirections.Right, edge.Color);
                    Add(
                        directions,
                        colors,
                        (throughLane * 2) + 1,
                        LineDirections.Left | LineDirections.Right,
                        edge.Color);
                    Add(directions, colors, shiftedLane * 2, LineDirections.Left | LineDirections.Down, edge.Color);
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
                    Connect(directions, colors, outgoingLane, nodeLane, LineDirections.Down, edge.Color);
                    break;
            }
        }

        var cells = new GraphCell[laneColumns * 2];
        for (var column = 0; column < cells.Length; column++)
        {
            cells[column] = new GraphCell(glyphs.For(directions[column]), colors[column]);
            if (directions[column] == LineDirections.None)
            {
                cells[column] = new GraphCell(' ', -1);
            }
        }

        // An unlabelled commit with exactly one child and one parent, both on its lane, is part of a straight line, not a node.
        var isLineCommit = !isDecorated && isOnLane && incomingCount == 1 && outgoingCount == 1;
        var nodeGlyph = outgoingCount > 1 ? glyphs.MergeNode : glyphs.Node;
        if (isLineCommit)
        {
            nodeGlyph = glyphs.LineNode;
        }

        cells[nodeLane * 2] = new GraphCell(nodeGlyph, row.NodeColor);
        return cells;
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

    // The first line in a cell decides its colour (a through lane keeps its own where a connection crosses it).
    private static void Add(LineDirections[] directions, int[] colors, int column, LineDirections added, int color)
    {
        if (directions[column] == LineDirections.None)
        {
            colors[column] = color;
        }

        directions[column] |= added;
    }
}
