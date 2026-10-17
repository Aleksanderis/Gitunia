using Gitunia.Core.Graph;
using Gitunia.Core.Lists;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Pick;

/// <summary>Turns a Visual range of graph list rows into the hashes of the commits it covers.</summary>
public static class PickRange
{
    /// <summary>
    /// The commits of the rows, newest first as the graph shows them. Reserved leading rows and rows past the loaded
    /// end are left out, so a range that reaches beyond the graph never reads outside it.
    /// </summary>
    public static IReadOnlyList<string> Hashes(CommitGraph graph, RowRange range)
    {
        var first = Math.Max(0, GraphListSource.CommitIndexOf(range.First));
        var last = Math.Min(graph.Count - 1, GraphListSource.CommitIndexOf(range.Last));
        if (last < first)
        {
            return [];
        }

        return [.. Enumerable.Range(first, last - first + 1).Select(index => graph[index].Id.ToString())];
    }
}
