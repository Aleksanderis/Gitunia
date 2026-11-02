using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class GraphBranchLaneTests
{
    private static readonly int[] MainRows = [0, 1, 12, 13, 14, 15, 16];
    private static readonly int[] BranchTipRows = [2, 3, 4, 5];
    private static readonly int[] LastBranchRows = [9, 10, 11];
    private static readonly int[] BelowFork = [13, 14, 15, 16, 17];
    private static readonly int[] MainTopCommits = [1, 2];

    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 3)];

    [Fact]
    public void GivenBranchesOnConsecutiveCommitsOfOneChain_WhenLayingOut_ThenTheChainStaysOneStraightLane()
    {
        // 1 (main), 2 (release), 3 (develop) and 4 (hotfix) are consecutive: the names are labels on main's line.
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3, 4), Commit(4, 5), Commit(5) };
        var tips = Tips(Tip(1, "main"), Tip(2, "release"), Tip(3, "develop"), Tip(4, "hotfix"));

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Equal([0, 0, 0, 0, 0], rows.Select(row => row.Lane));
        Assert.Single(rows.Select(row => row.NodeColor).Distinct());
        Assert.All(rows.SelectMany(row => row.Edges), edge => Assert.Equal(rows[0].NodeColor, edge.Color));
    }

    [Fact]
    public void GivenAMainLineWithParallelBranches_WhenLayingOut_ThenEachBranchKeepsItsLaneAndColourFromTipToFork()
    {
        var commits = Demo();
        var rows = new GraphLayoutBuilder(DemoTips()).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        // Main (1 docs, 2 merge, 13 and below) is lane 0 all the way; the merged topic and feat-c, feat-b, feat-a
        // (newest tip first) are lanes 1 to 4 from their tips down to the fork commit 13.
        Assert.Equal([0, 0, 1, 2, 3, 4, 2, 3, 4, 2, 3, 4, 0, 0, 0, 0, 0], rows.Select(row => row.Lane));
        var mainColour = rows[0].NodeColor;
        Assert.All(MainRows, index => Assert.Equal(mainColour, rows[index].NodeColor));
        var branchColours = BranchTipRows.Select(index => rows[index].NodeColor).ToArray();
        Assert.Equal(4, branchColours.Append(mainColour).Distinct().Count() - 1);
        Assert.Equal(rows[3].NodeColor, rows[6].NodeColor);
        Assert.Equal(rows[3].NodeColor, rows[9].NodeColor);
        Assert.Equal(rows[4].NodeColor, rows[10].NodeColor);
        Assert.Equal(rows[5].NodeColor, rows[11].NodeColor);
    }

    [Fact]
    public void GivenAMainLineWithParallelBranches_WhenLayingOut_ThenTheBranchLanesJoinMainAtTheForkCommit()
    {
        var rows = new GraphLayoutBuilder(DemoTips()).AppendRange(Demo());

        var fork = rows[12];
        Assert.Equal(0, fork.Lane);
        var joining = fork.Edges.Where(edge => edge.Kind == GraphEdgeKind.Incoming).Select(edge => edge.FromLane);
        Assert.Equal([0, 1, 2, 3, 4], joining);
        // Each last commit of a branch still draws its own straight lane down to the fork.
        foreach (var index in LastBranchRows)
        {
            var outgoing = rows[index].Edges.Single(edge => edge.Kind == GraphEdgeKind.Outgoing);
            Assert.Equal(rows[index].Lane, outgoing.ToLane);
        }
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 })]
    [InlineData(new[] { 4, 5, 6, 1, 2, 3 })]
    [InlineData(new[] { 6, 1, 5, 2, 4, 3 })]
    [InlineData(new[] { 5, 4, 1, 2, 3, 6 })]
    public void GivenTheTipsInAnyOrder_WhenLayingOut_ThenTheLeftmostLaneGoesToTheBottomAndTheOthersJoinIt(int[] topRows)
    {
        // The same history with its first rows reordered, as it comes when all commits share one timestamp.
        var commits = DemoWithTopRows(topRows);
        var rows = new GraphLayoutBuilder(DemoTips()).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        var firstLane = rows[0].Lane;
        Assert.Equal(0, firstLane);

        // The fork commit and everything below it stay in the lane of the first row (the newest tip).
        foreach (var number in BelowFork)
        {
            var index = Array.FindIndex(commits, commit => commit.Id == Id(number));
            Assert.Equal(firstLane, rows[index].Lane);
        }

        // Every other line (main above the fork, the merged topic, the feature branches) has one lane and bends into
        // the leftmost one at the fork commit.
        var forkIndex = Array.FindIndex(commits, commit => commit.Id == Id(13));
        var edges = rows[forkIndex].Edges;
        var incoming = edges.Where(edge => edge.Kind == GraphEdgeKind.Incoming).Select(edge => edge.FromLane).ToList();
        Assert.Equal(5, incoming.Distinct().Count());
        Assert.Contains(firstLane, incoming);
        foreach (var number in BranchTipRows.Select(row => row + 1))
        {
            var chain = ChainOf(number);
            var lanes = Enumerable.Range(0, commits.Length)
                .Where(index => chain.Contains(commits[index].Id))
                .Select(index => rows[index].Lane)
                .Distinct();
            Assert.Single(lanes);
        }

        var mainTop = MainTopCommits
            .Select(number => rows[Array.FindIndex(commits, commit => commit.Id == Id(number))].Lane)
            .Distinct();
        Assert.Single(mainTop);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistoryAndBranchTips_WhenLayingOut_ThenEveryCommitKeepsTheLaneOfItsFirstParent(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var random = new Random(seed);
        var tips = commits
            .Where(_ => random.Next(3) == 0)
            .ToDictionary(commit => commit.Id, _ => $"branch-{random.Next(6)}");

        var rows = new GraphLayoutBuilder(tips).AppendRange(commits);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        for (var index = 0; index < rows.Count; index++)
        {
            if (commits[index].Parents.Count > 0)
            {
                var first = rows[index].Edges.First(edge => edge.Kind == GraphEdgeKind.Outgoing);
                Assert.Equal(rows[index].Lane, first.ToLane);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistoryAndBranchTips_WhenAppendingInChunks_ThenRowsEqualTheFullLayout(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var random = new Random(seed);
        var tips = commits
            .Where(_ => random.Next(4) == 0)
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
    }

    // The history of the owner's demo repository, newest first: 1 docs (main), 2 merge of topic 3, feat-c 4 7 10,
    // feat-b 5 8 11, feat-a 6 9 12, all forked from 13 "cli", then 14 hotfix, 15 develop, 16 release, 17 initial.
    private static CommitInfo[] Demo() => DemoWithTopRows([1, 2, 3, 4, 5, 6]);

    private static CommitInfo[] DemoWithTopRows(int[] topRows)
    {
        var top = new Dictionary<int, CommitInfo>
        {
            [1] = Commit(1, 2),
            [2] = Commit(2, 13, 3),
            [3] = Commit(3, 13),
            [4] = Commit(4, 7),
            [5] = Commit(5, 8),
            [6] = Commit(6, 9),
        };
        var commits = topRows.Select(number => top[number]).ToList();

        // The second and third commits of the three feature branches follow, round-robin, c b a.
        commits.AddRange([Commit(7, 10), Commit(8, 11), Commit(9, 12), Commit(10, 13), Commit(11, 13), Commit(12, 13)]);
        commits.AddRange([Commit(13, 14), Commit(14, 15), Commit(15, 16), Commit(16, 17), Commit(17)]);

        return [.. commits];
    }

    private static HashSet<ObjectId> ChainOf(int tip) => tip switch
    {
        3 => [Id(3)],
        4 => [Id(4), Id(7), Id(10)],
        5 => [Id(5), Id(8), Id(11)],
        _ => [Id(6), Id(9), Id(12)],
    };

    private static Dictionary<ObjectId, string> DemoTips() => Tips(
        Tip(1, "main"),
        Tip(3, "merged-topic"),
        Tip(4, "feat-c"),
        Tip(5, "feat-b"),
        Tip(6, "feat-a"),
        Tip(14, "hotfix"),
        Tip(15, "develop"),
        Tip(16, "release"));

    private static Dictionary<ObjectId, string> Tips(params KeyValuePair<ObjectId, string>[] tips) => new(tips);

    private static KeyValuePair<ObjectId, string> Tip(int commit, string branch) => new(Id(commit), branch);
}
