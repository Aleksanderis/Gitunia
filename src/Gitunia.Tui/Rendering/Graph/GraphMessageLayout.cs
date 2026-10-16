namespace Gitunia.Tui.Rendering.Graph;

/// <summary>Where the pieces of one graph row start: its lane cells, the blank gap after them and the columns left for the message.</summary>
/// <param name="Cells">The lane cells of the row.</param>
/// <param name="UsedLaneWidth">Columns of the cells that carry a line.</param>
/// <param name="Gap">Blank columns between the lines and the message.</param>
/// <param name="MessageWidth">Columns of the message (ref labels and subject).</param>
public readonly record struct GraphMessageLayout(GraphCell[] Cells, int UsedLaneWidth, int Gap, int MessageWidth);
