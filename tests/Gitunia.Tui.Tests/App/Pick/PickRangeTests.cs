using Gitunia.Core.Graph;
using Gitunia.Core.Lists;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.Tests.Rendering.Graph;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.App.Pick;

[Trait("Os", "Independent")]
public class PickRangeTests
{
    private static readonly int Lead = ScreenLayout.ReservedLeadingRows;

    [Fact]
    public void GivenARangeInsideTheGraph_WhenConverting_ThenTheCommitsComeNewestFirstInEitherDirection()
    {
        var graph = Graph();

        var down = PickRange.Hashes(graph, new RowRange(Lead + 1, Lead + 2));
        var up = PickRange.Hashes(graph, new RowRange(Lead + 2, Lead + 1));

        Assert.Equal([graph[1].Id.ToString(), graph[2].Id.ToString()], down);
        Assert.Equal(down, up);
    }

    [Fact]
    public void GivenARangeReachingPastBothEnds_WhenConverting_ThenItIsClampedToTheLoadedCommits()
    {
        var graph = Graph();

        var hashes = PickRange.Hashes(graph, new RowRange(0, Lead + 50));

        Assert.Equal(3, hashes.Count);
    }

    [Fact]
    public void GivenARangeOnlyOnReservedRows_WhenConverting_ThenNothingIsReturned()
    {
        Assert.Empty(PickRange.Hashes(Graph(), new RowRange(-2, -1)));
    }

    private static CommitGraph Graph() => CommitFactory.Graph(
        CommitFactory.Commit(3, "three", [2]),
        CommitFactory.Commit(2, "two", [1]),
        CommitFactory.Commit(1, "one", []));
}
