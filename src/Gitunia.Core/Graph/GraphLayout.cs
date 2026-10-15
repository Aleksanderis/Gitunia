using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Graph;

/// <summary>Batch form of the graph layout: the same result as feeding the commits to a <see cref="GraphLayoutBuilder"/>.</summary>
public static class GraphLayout
{
    /// <param name="commits">Commits ordered children before parents.</param>
    /// <param name="headCommit">The commit of the checked-out branch whose first-parent chain gets lane 0; null when HEAD is detached.</param>
    /// <param name="aboveHead">
    /// The commits whose first-parent chain reaches <paramref name="headCommit"/> (branches ahead of it), or null if not known.
    /// </param>
    public static IReadOnlyList<GraphRow> Compute(
        IEnumerable<CommitInfo> commits,
        ObjectId? headCommit = null,
        IReadOnlySet<ObjectId>? aboveHead = null)
    {
        var builder = new GraphLayoutBuilder(headCommit, aboveHead);
        builder.AppendRange(commits);
        return builder.Rows;
    }
}
