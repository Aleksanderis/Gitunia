using Gitunia.Core.Graph;

namespace Gitunia.Tui.Rendering;

/// <summary>Turns the edges of a <see cref="GraphRow"/> into characters: two columns per lane, the second one carries connecting lines.</summary>
public static class LaneCellBuilder
{
    /// <param name="row">The layout of the row.</param>
    /// <param name="laneColumns">Lanes shown; lanes further right are drawn in the last column.</param>
    /// <param name="glyphs">The characters to use.</param>
    public static GraphCell[] Build(GraphRow row, int laneColumns, GraphGlyphSet glyphs)
    {
        var cells = new GraphCell[laneColumns * 2];
        Array.Fill(cells, new GraphCell(' ', -1));

        var nodeLane = Math.Min(row.Lane, laneColumns - 1);
        var outgoingCount = 0;
        foreach (var edge in row.Edges)
        {
            switch (edge.Kind)
            {
                case GraphEdgeKind.Through:
                    var throughLane = Math.Min(edge.FromLane, laneColumns - 1);
                    cells[throughLane * 2] = new GraphCell(glyphs.Vertical, throughLane);
                    break;
                case GraphEdgeKind.Incoming:
                    Connect(cells, Math.Min(edge.FromLane, laneColumns - 1), nodeLane, isIncoming: true, glyphs);
                    break;
                default:
                    outgoingCount++;
                    Connect(cells, Math.Min(edge.ToLane, laneColumns - 1), nodeLane, isIncoming: false, glyphs);
                    break;
            }
        }

        cells[nodeLane * 2] = new GraphCell(outgoingCount > 1 ? glyphs.MergeNode : glyphs.Node, nodeLane);
        return cells;
    }

    private static void Connect(GraphCell[] cells, int otherLane, int nodeLane, bool isIncoming, GraphGlyphSet glyphs)
    {
        if (otherLane == nodeLane)
        {
            return;
        }

        var from = Math.Min(otherLane, nodeLane);
        var to = Math.Max(otherLane, nodeLane);
        for (var column = (from * 2) + 1; column < to * 2; column++)
        {
            var existing = cells[column].Glyph;
            if (existing == ' ')
            {
                cells[column] = new GraphCell(glyphs.Horizontal, otherLane);
            }
            else if (existing == glyphs.Vertical)
            {
                cells[column] = new GraphCell(glyphs.Cross, cells[column].Lane);
            }
        }

        var isRight = otherLane > nodeLane;
        var corner = isIncoming
            ? (isRight ? glyphs.IncomingFromRight : glyphs.IncomingFromLeft)
            : (isRight ? glyphs.OutgoingToRight : glyphs.OutgoingToLeft);
        var opposite = isIncoming
            ? (isRight ? glyphs.OutgoingToRight : glyphs.OutgoingToLeft)
            : (isRight ? glyphs.IncomingFromRight : glyphs.IncomingFromLeft);

        // A lane that continues through the row and receives the node's line, or ends at the node and is reused for a
        // parent in the same row, is one tee instead of a corner that would cut the lane.
        var cornerCell = cells[otherLane * 2].Glyph;
        var isTee = cornerCell == glyphs.Vertical || (cornerCell == opposite && opposite != corner);
        cells[otherLane * 2] = new GraphCell(isTee ? (isRight ? glyphs.TeeLeft : glyphs.TeeRight) : corner, otherLane);
    }
}
