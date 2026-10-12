using System.Collections.Immutable;
using Gitunia.Core.Graph;

namespace Gitunia.Core.Search;

/// <summary>Scans the loaded rows for a query in bounded slices, so a caller can interleave scanning with input handling.</summary>
public static class GraphSearchScanner
{
    /// <param name="query">What to look for.</param>
    /// <param name="graph">The loaded rows.</param>
    /// <param name="start">First row to look at.</param>
    /// <param name="maximumCount">Rows to look at in this slice.</param>
    public static GraphScanResult Scan(SearchQuery query, CommitGraph graph, int start, int maximumCount)
    {
        var end = Math.Min(graph.Count, start + Math.Max(1, maximumCount));
        var matches = ImmutableArray.CreateBuilder<int>();
        for (var index = start; index < end; index++)
        {
            if (GraphSearchMatcher.IsMatch(query, graph[index]))
            {
                matches.Add(index);
            }
        }

        return new GraphScanResult(matches.ToImmutable(), Math.Max(start, end));
    }
}
