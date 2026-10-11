using Gitunia.Git;

namespace Gitunia.Core.Graph;

/// <summary>Batch form of the graph layout: the same result as feeding the commits to a <see cref="GraphLayoutBuilder"/>.</summary>
public static class GraphLayout
{
    /// <param name="commits">Commits ordered children before parents.</param>
    public static IReadOnlyList<GraphRow> Compute(IEnumerable<CommitInfo> commits)
    {
        var builder = new GraphLayoutBuilder();
        builder.AppendRange(commits);
        return builder.Rows;
    }
}
