using Gitunia.Core.Graph;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphBranchOrderTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 40)];

    [Fact]
    public void GivenThreeBranchesWithDifferentTipDates_WhenLayingOut_ThenTheNewestTipIsLeftmostThenTheNextNewest()
    {
        // 1, 2 and 3 are the tips, newest first (the log order); each branch runs on to its own commit, all meet at 7.
        var commits = new[]
        {
            Commit(1, 4), Commit(2, 5), Commit(3, 6), Commit(4, 7), Commit(5, 7), Commit(6, 7), Commit(7),
        };

        var rows = GraphLayout.Compute(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([0, 1, 2, 0, 1, 2, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenTheOldestTipIsTheOneTheCheckedOutBranchPointsAt_WhenLayingOut_ThenItGetsNoLaneOfItsOwn()
    {
        // Layout never sees HEAD: 3 (the checked-out branch) is listed last of the tips, so it is the rightmost.
        var commits = new[]
        {
            Commit(1, 4), Commit(2, 5), Commit(3, 6), Commit(4, 7), Commit(5, 7), Commit(6, 7), Commit(7),
        };

        var rows = GraphLayout.Compute(commits);

        Assert.Equal(2, rows[2].Lane);
    }

    [Fact]
    public void GivenTipsOfOneParent_WhenLayingOut_ThenEachTipHasItsOwnLaneDownToTheParent()
    {
        var commits = new[] { Commit(1, 4), Commit(2, 4), Commit(3, 4), Commit(4, 5), Commit(5) };

        var rows = GraphLayout.Compute(commits);

        Assert.Equal([0, 1, 2, 0, 0], rows.Select(row => row.Lane));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenAnyChunking_WhenAppendingIncrementally_ThenRowsEqualTheFullLayout(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var full = GraphLayout.Compute(commits);
        var random = new Random(seed);
        var builder = new GraphLayoutBuilder();

        var position = 0;
        while (position < commits.Count)
        {
            var size = Math.Min(random.Next(1, 40), commits.Count - position);
            builder.AppendRange(commits.Skip(position).Take(size));
            position += size;
            Assert.True(GraphRowComparer.AreEqual(full.Take(position).ToArray(), builder.Rows));
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistory_WhenLayingOut_ThenTheTopologyIsValid(int seed)
    {
        var commits = RandomHistory(seed, count: 400, window: 12);

        var rows = GraphLayout.Compute(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
    }
}
