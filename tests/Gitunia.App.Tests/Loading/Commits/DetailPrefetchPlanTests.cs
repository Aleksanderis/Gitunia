using Gitunia.App.Loading.Commits;
using Gitunia.Core.Graph;
using Xunit;

namespace Gitunia.App.Tests.Loading.Commits;

[Trait("Os", "Independent")]
public class DetailPrefetchPlanTests
{
    [Fact]
    public void GivenRowInTheMiddle_WhenPlanning_ThenNearestFirstAlternatingAheadAndBehind()
    {
        var graph = Chain(10);

        var plan = DetailPrefetchPlan.Around(graph, 5);

        Assert.Equal([CommitFactory.Id(6), CommitFactory.Id(4), CommitFactory.Id(7), CommitFactory.Id(8)], plan);
    }

    [Fact]
    public void GivenFirstRow_WhenPlanning_ThenOnlyRowsBelowAreIncluded()
    {
        var plan = DetailPrefetchPlan.Around(Chain(10), 0);

        Assert.Equal([CommitFactory.Id(1), CommitFactory.Id(2), CommitFactory.Id(3)], plan);
    }

    [Fact]
    public void GivenLastRow_WhenPlanning_ThenOnlyTheRowAboveIsIncluded()
    {
        var plan = DetailPrefetchPlan.Around(Chain(10), 9);

        Assert.Equal([CommitFactory.Id(8)], plan);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void GivenIndexOutsideTheRows_WhenPlanning_ThenNothingIsPlanned(int index)
    {
        Assert.Empty(DetailPrefetchPlan.Around(Chain(10), index));
    }

    private static CommitGraph Chain(int count)
    {
        var commits = Enumerable.Range(0, count)
            .Select(number => CommitFactory.Commit(number, $"commit {number}", number + 1 < count ? [number + 1] : []))
            .ToArray();
        return CommitFactory.Graph(commits);
    }
}
