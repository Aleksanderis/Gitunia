using Gitunia.Core.Graph;
using Gitunia.Core.Jump;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Graph;

/// <summary>The commits of the graph as quick-jump rows: the subject, within the columns the row layout leaves it.</summary>
/// <param name="graph">The loaded commits.</param>
/// <param name="options">The layout the rows are drawn with.</param>
public sealed class GraphJumpRows(CommitGraph graph, GraphRowOptions options) : IJumpRows
{
    public int Count => graph.Count;

    public int EllipsisWidth { get; } = TextWidth.Of(options.Glyphs.Ellipsis);

    public string TextOf(int row) => graph[row].Subject;

    public int ColumnsOf(int row) => GraphRowRenderer.SubjectColumns(graph[row], options);
}
