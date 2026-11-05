using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>
/// Turns the edges of a <see cref="GraphRow"/> into characters: two columns per lane, the second one carries
/// connecting lines. Each cell collects the directions its lines leave through and the glyph set maps the
/// combination to a character, so overlapping lines (forks, joins, crossings) always draw correctly.
/// </summary>
public static class LaneCellBuilder
{
    private const LineDirections AllSides = LineDirections.Up | LineDirections.Down | LineDirections.Left | LineDirections.Right;

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
        Span<PendingStep> steps = stackalloc PendingStep[row.Edges.Count];
        var stepCount = 0;
        foreach (var edge in row.Edges)
        {
            switch (edge.Kind)
            {
                case GraphEdgeKind.Through:
                    var throughLane = Math.Min(edge.FromLane, laneColumns - 1);
                    Add(directions, colors, throughLane * 2, LineDirections.Up | LineDirections.Down, edge.Color);
                    break;
                case GraphEdgeKind.Incoming:
                    incomingCount++;
                    isOnLane &= edge.FromLane == row.Lane;
                    var incomingLane = Math.Min(edge.FromLane, laneColumns - 1);
                    if (Connect(directions, colors, incomingLane, nodeLane, edge.Color))
                    {
                        steps[stepCount++] = new PendingStep(incomingLane, LineDirections.Up, edge.Color);
                    }

                    break;
                default:
                    outgoingCount++;
                    isOnLane &= edge.ToLane == row.Lane;
                    var outgoingLane = Math.Min(edge.ToLane, laneColumns - 1);
                    if (Connect(directions, colors, outgoingLane, nodeLane, edge.Color))
                    {
                        steps[stepCount++] = new PendingStep(outgoingLane, LineDirections.Down, edge.Color);
                    }

                    break;
            }
        }

        for (var index = 0; index < stepCount; index++)
        {
            FinishStep(directions, colors, steps[..stepCount], index, nodeLane);
        }

        var cells = new GraphCell[laneColumns * 2];
        for (var column = 0; column < cells.Length; column++)
        {
            // A lane crossing a connecting line stays a plain vertical: the line passes under it instead of a cross.
            var cellDirections = directions[column] == AllSides ? LineDirections.Up | LineDirections.Down : directions[column];
            cells[column] = new GraphCell(glyphs.For(cellDirections), colors[column]);
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

    // A line between the node and another lane: horizontal in between, and a diagonal step in the connector cell next
    // to the other lane (FinishStep), so the line leaves or enters the lane at an angle instead of a corner (#1230).
    // Returns false when the other lane is the node's own.
    private static bool Connect(
        LineDirections[] directions,
        int[] colors,
        int otherLane,
        int nodeLane,
        int color)
    {
        if (otherLane == nodeLane)
        {
            return false;
        }

        var from = Math.Min(otherLane, nodeLane);
        var to = Math.Max(otherLane, nodeLane);
        var isRight = otherLane > nodeLane;

        // The run stops short of the diagonal cell, the one beside the other lane.
        var first = isRight ? (from * 2) + 1 : (from * 2) + 2;
        var last = isRight ? (to * 2) - 2 : (to * 2) - 1;
        for (var column = first; column <= last; column++)
        {
            Add(directions, colors, column, LineDirections.Left | LineDirections.Right, color);
        }

        return true;
    }

    // The diagonal goes in the connector cell beside the other lane when nothing else uses that cell; otherwise the
    // old corner at the lane (a line running through the cell, or another diagonal, would be cut by it).
    private static void FinishStep(
        LineDirections[] directions,
        int[] colors,
        ReadOnlySpan<PendingStep> steps,
        int index,
        int nodeLane)
    {
        var step = steps[index];
        var isRight = step.Lane > nodeLane;
        var diagonalColumn = isRight ? (step.Lane * 2) - 1 : (step.Lane * 2) + 1;
        if (directions[diagonalColumn] == LineDirections.None && !IsLaneShared(steps, index))
        {
            // Down-right and up-left read as a backslash, up-right and down-left as a slash.
            var isDown = step.Vertical == LineDirections.Down;
            Add(directions, colors, diagonalColumn, isRight == isDown ? LineDirections.Backslash : LineDirections.Slash, step.Color);
            return;
        }

        Add(directions, colors, diagonalColumn, LineDirections.Left | LineDirections.Right, step.Color);
        Add(directions, colors, step.Lane * 2, step.Vertical | (isRight ? LineDirections.Left : LineDirections.Right), step.Color);
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

    // Lines in and out of the same lane (a lane ending here and starting again) would share one diagonal cell.
    private static bool IsLaneShared(ReadOnlySpan<PendingStep> steps, int index)
    {
        for (var other = 0; other < steps.Length; other++)
        {
            if (other != index && steps[other].Lane == steps[index].Lane)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct PendingStep(int Lane, LineDirections Vertical, int Color);
}
