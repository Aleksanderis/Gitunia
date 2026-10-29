using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphBranchLaneTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 40)];

    [Fact]
    public void GivenBranchesOnConsecutiveCommitsOfOneChain_WhenLayingOut_ThenEachBranchHasItsOwnLane()
    {
        // 1 (a), 2 (b) and 3 (c) are consecutive commits, each carrying another branch; 4 has none.
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3, 4), Commit(4) };
        var tips = Tips(Tip(1, "a"), Tip(2, "b"), Tip(3, "c"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows, allowsOwnLane: true));
        Assert.Equal([0, 1, 0, 0], rows.Select(row => row.Lane));
        Assert.NotEqual(rows[0].Lane, rows[1].Lane);
        Assert.NotEqual(rows[1].Lane, rows[2].Lane);
    }

    [Fact]
    public void GivenAnotherBranchTipReachedFromALane_WhenLayingOut_ThenTheLaneBendsIntoTheNewLaneAtThatRow()
    {
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var tips = Tips(Tip(1, "feature"), Tip(2, "main"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal(1, rows[1].Lane);
        var incoming = Assert.Single(rows[1].Edges, edge => edge.Kind == GraphEdgeKind.Incoming);
        Assert.Equal(0, incoming.FromLane);
        Assert.Equal(1, incoming.ToLane);
        Assert.Equal(rows[0].NodeColor, incoming.Color);
        Assert.NotEqual(rows[0].NodeColor, rows[1].NodeColor);
    }

    [Fact]
    public void GivenALocalAndARemoteBranchOfTheSameNameOnConsecutiveCommits_WhenLayingOut_ThenTheyShareTheLane()
    {
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };
        var tips = Tips(Tip(1, "topic"), Tip(2, "topic"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal([0, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenAMergedBranch_WhenLayingOut_ThenItsTipSitsInTheLaneTheMergeOpenedForIt()
    {
        // 1 (main) merges 2 and 3 (feature); both lead to 4.
        var commits = new[] { Commit(1, 2, 3), Commit(2, 4), Commit(3, 4), Commit(4) };
        var tips = Tips(Tip(1, "main"), Tip(3, "feature"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows, allowsOwnLane: true));
        Assert.Equal([0, 0, 1, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenALaneNoBranchStartedAndATipItLeadsTo_WhenLayingOut_ThenTheTipStartsItsOwnLane()
    {
        // 1 has no branch name, 2 is the tip of "main".
        var commits = new[] { Commit(1, 2), Commit(2) };

        var rows = new GraphLayoutBuilder(Tips(Tip(2, "main"))).AppendRange(commits);

        Assert.NotEqual(rows[0].Lane, rows[1].Lane);
    }

    [Fact]
    public void GivenNoBranchNames_WhenLayingOut_ThenTheLayoutIsTheLeftmostFreeLaneOne()
    {
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3) };

        var rows = new GraphLayoutBuilder().AppendRange(commits);

        Assert.Equal([0, 0, 0], rows.Select(row => row.Lane));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistoryAndBranchTips_WhenLayingOut_ThenTheTopologyIsValidAndChunkingDoesNotMatter(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var random = new Random(seed);
        var tips = commits.Where(_ => random.Next(4) == 0)
            .ToDictionary(commit => commit.Id, _ => $"branch-{random.Next(6)}");
        var full = new GraphLayoutBuilder(tips).AppendRange(commits);
        var builder = new GraphLayoutBuilder(tips);

        var position = 0;
        while (position < commits.Count)
        {
            var size = Math.Min(random.Next(1, 40), commits.Count - position);
            builder.AppendRange(commits.Skip(position).Take(size));
            position += size;
            Assert.True(GraphRowComparer.AreEqual(full.Take(position).ToArray(), builder.Rows));
        }

        Assert.Empty(GraphTopologyValidator.Validate(commits, full, allowsOwnLane: true));
    }

    [Fact]
    public void GivenAnOctopusMergeWithANamedParent_WhenLayingOut_ThenThatParentKeepsTheLaneTheMergeOpened()
    {
        var commits = new[] { Commit(1, 2, 3, 4), Commit(2, 5), Commit(3, 5), Commit(4, 5), Commit(5) };
        var tips = Tips(Tip(1, "main"), Tip(3, "x"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows, allowsOwnLane: true));
        var lane = rows[0].Edges.Single(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.ToLane == rows[2].Lane);
        Assert.Equal(rows[2].Lane, lane.ToLane);
        Assert.Equal(1, rows[2].Edges.Count(edge => edge.Kind == GraphEdgeKind.Incoming));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenAChainWithRandomBranchTips_WhenLayingOut_ThenEveryChangeOfBranchChangesTheLane(int seed)
    {
        var random = new Random(seed);
        var commits = Enumerable.Range(1, 60).Select(number => Commit(number, number + 1)).Append(Commit(61)).ToArray();
        var names = Enumerable.Range(1, 61)
            .Select(_ => random.Next(3) == 0 ? $"branch-{random.Next(4)}" : null)
            .ToArray();
        var tips = Enumerable.Range(1, 61).Where(number => names[number - 1] is not null)
            .ToDictionary(Id, number => names[number - 1]!);

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        // The oracle: one lane is open at a time, a commit with another branch name moves to the leftmost free lane.
        var expectedLane = 0;
        var branch = (string?)null;
        for (var index = 0; index < rows.Count; index++)
        {
            var name = names[index];
            if (index > 0 && name is not null && name != branch)
            {
                expectedLane = expectedLane == 0 ? 1 : 0;
            }

            branch = index == 0 || (name is not null && name != branch) ? name : branch;
            Assert.Equal(expectedLane, rows[index].Lane);
        }

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows, allowsOwnLane: true));
    }

    private static Dictionary<ObjectId, string> Tips(params KeyValuePair<ObjectId, string>[] tips) => new(tips);

    private static KeyValuePair<ObjectId, string> Tip(int commit, string branch) => new(Id(commit), branch);
}
