using Gitunia.Core.Graph;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

public class GraphLayoutPropertyTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 40)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistory_WhenLayingOut_ThenEdgesDescribeTheTopologyExactly(int seed)
    {
        var commits = RandomHistory(seed, count: 400, window: 12);

        var rows = GraphLayout.Compute(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenSameCommits_WhenLayingOutTwice_ThenResultsAreIdentical(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);

        Assert.True(GraphRowComparer.AreEqual(GraphLayout.Compute(commits), GraphLayout.Compute(commits)));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenAnyChunking_WhenAppendingIncrementally_ThenRowsEqualTheFullLayout(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var full = GraphLayout.Compute(commits);
        var random = new Random(seed);
        var builder = new GraphLayoutBuilder();
        var collected = new List<GraphRow>();

        var position = 0;
        while (position < commits.Count)
        {
            var size = Math.Min(random.Next(1, 40), commits.Count - position);
            collected.AddRange(builder.AppendRange(commits.Skip(position).Take(size)));
            position += size;
            Assert.True(GraphRowComparer.AreEqual(full.Take(position).ToArray(), builder.Rows));
        }

        Assert.True(GraphRowComparer.AreEqual(full, collected));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenParentsWithinAWindow_WhenLayingOut_ThenLaneCountStaysWithinTheWindow(int seed)
    {
        const int Window = 8;
        var commits = RandomHistory(seed, count: 500, window: Window);

        var rows = GraphLayout.Compute(commits);

        // Open lanes wait for distinct, still-unseen commits within the window; one row adds at most three more parents.
        Assert.All(rows, row => Assert.InRange(row.LaneCount, 1, Window + 3));
    }

    [Fact]
    public void GivenLinearHistory_WhenLayingOut_ThenTheGraphNeverNeedsASecondLane()
    {
        var commits = Enumerable.Range(1, 1000).Select(number => number == 1000 ? Commit(number) : Commit(number, number + 1)).ToArray();

        var rows = GraphLayout.Compute(commits);

        Assert.All(rows, row => Assert.Equal(1, row.LaneCount));
    }

    [Fact]
    public void GivenHundredThousandCommits_WhenLayingOut_ThenAllRowsExistAndLanesStayNarrow()
    {
        var commits = RandomHistory(seed: 7, count: 100_000, window: 16);

        var builder = new GraphLayoutBuilder();
        builder.AppendRange(commits);

        Assert.Equal(100_000, builder.Rows.Count);
        Assert.InRange(builder.Rows.Max(row => row.LaneCount), 1, 19);
    }
}
