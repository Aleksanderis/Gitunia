using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering;

/// <summary>Builds commits and graph entries for rendering tests without a repository.</summary>
public static class CommitFactory
{
    /// <summary>An id whose first seven hex digits are the number, so the short hash shown in the graph is readable.</summary>
    public static ObjectId Id(int number)
    {
        Assert.True(ObjectId.TryParse(number.ToString("x7", System.Globalization.CultureInfo.InvariantCulture).PadRight(40, '0'), out var id));
        return id;
    }

    public static CommitInfo Commit(int number, string subject, int[] parents, params CommitDecoration[] decorations) =>
        new(
            Id(number),
            parents.Select(Id).ToArray(),
            "Author",
            "author@example.com",
            DateTimeOffset.UnixEpoch,
            subject,
            decorations);

    /// <summary>Lays out the commits (children first) and pairs each with its row.</summary>
    public static CommitGraph Graph(params CommitInfo[] commits)
    {
        var rows = GraphLayout.Compute(commits);
        var graph = new CommitGraph();
        graph.AddRange(commits.Select((commit, index) =>
            new GraphEntry(commit.Id, commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, rows[index])).ToArray());
        return graph;
    }
}
