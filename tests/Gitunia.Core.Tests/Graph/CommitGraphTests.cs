using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

public class CommitGraphTests
{
    private static ObjectId Id(int number)
    {
        Assert.True(ObjectId.TryParse(number.ToString("x40", CultureInfo.InvariantCulture), out var id));
        return id;
    }

    private static GraphEntry Entry(int number, int laneCount)
    {
        var row = new GraphRow(Id(number), 0, laneCount, []);
        return new GraphEntry(Id(number), $"commit {number}", "Author", DateTimeOffset.UnixEpoch, [], row);
    }

    [Fact]
    public void GivenBatches_WhenAppending_ThenIndexesStayStable()
    {
        var graph = new CommitGraph();

        graph.AddRange([Entry(1, 1), Entry(2, 1)]);
        graph.AddRange([Entry(3, 1)]);

        Assert.Equal(3, graph.Count);
        Assert.Equal("commit 1", graph[0].Subject);
        Assert.Equal("commit 3", graph[2].Subject);
    }

    [Fact]
    public void GivenWiderRowsLater_WhenAppending_ThenMaximumLaneCountOnlyGrows()
    {
        var graph = new CommitGraph();

        graph.AddRange([Entry(1, 3)]);
        graph.AddRange([Entry(2, 1)]);

        Assert.Equal(3, graph.MaximumLaneCount);
    }

    [Fact]
    public void GivenCommit_WhenLookingUp_ThenIndexOrMinusOneWhenNotLoaded()
    {
        var graph = new CommitGraph();
        graph.AddRange([Entry(1, 1), Entry(2, 1)]);

        Assert.Equal(1, graph.IndexOf(Id(2)));
        Assert.Equal(-1, graph.IndexOf(Id(9)));
    }
}
