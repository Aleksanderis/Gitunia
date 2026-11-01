using Gitunia.Core.Graph;
using Gitunia.Git;

namespace Gitunia.App.Loading.Commits;

/// <summary>Picks which commits around the selected row are worth reading before they are selected.</summary>
public static class DetailPrefetchPlan
{
    /// <summary>Rows prefetched below the selection (the usual direction of travel).</summary>
    public const int RowsAhead = 3;

    /// <summary>Rows prefetched above the selection.</summary>
    public const int RowsBehind = 1;

    /// <param name="graph">The rows of the tab.</param>
    /// <param name="index">The selected row.</param>
    /// <returns>The commits of the nearest rows, nearest first, alternating ahead and behind; empty when <paramref name="index"/> is not a row.</returns>
    public static IReadOnlyList<ObjectId> Around(CommitGraph graph, int index)
    {
        if (index < 0 || index >= graph.Count)
        {
            return [];
        }

        var commits = new List<ObjectId>(RowsAhead + RowsBehind);
        // Ahead is the longer reach, so it bounds the loop.
        for (var distance = 1; distance <= RowsAhead; distance++)
        {
            if (index + distance < graph.Count)
            {
                commits.Add(graph[index + distance].Id);
            }

            if (distance <= RowsBehind && index - distance >= 0)
            {
                commits.Add(graph[index - distance].Id);
            }
        }

        return commits;
    }
}
