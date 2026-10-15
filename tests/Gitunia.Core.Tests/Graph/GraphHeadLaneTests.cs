using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

public class GraphHeadLaneTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 40)];

    [Fact]
    public void GivenOtherTipsListedBeforeHead_WhenLayingOutWithHead_ThenTheHeadChainIsStraightInLaneZero()
    {
        // 1 (origin/main) and 2 (feature) come before 3 (HEAD); all meet at 4.
        var commits = new[] { Commit(1, 4), Commit(2, 4), Commit(3, 4), Commit(4, 5), Commit(5) };

        var rows = GraphLayout.Compute(commits, Id(3));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([1, 2, 0, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenBranchAheadOfHead_WhenLayingOut_ThenTheAheadTipAndHeadShareStraightLaneZero()
    {
        // 1 (origin/main) is one commit ahead of HEAD 2; 3 is a merged feature branch tip; all meet at 4 (issue #307).
        var commits = new[] { Commit(1, 2), Commit(2, 4, 3), Commit(3, 4), Commit(4, 5), Commit(5) };

        var rows = GraphLayout.Compute(commits, Id(2));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([0, 0, 1, 0, 0], rows.Select(row => row.Lane));
        Assert.All(rows[0].Edges, edge => Assert.Equal(0, edge.ToLane));
    }

    [Fact]
    public void GivenBranchTwoCommitsAheadOfHead_WhenLayingOut_ThenTheTipBendsInFromLaneOne()
    {
        var commits = new[] { Commit(1, 2), Commit(2, 3), Commit(3, 4), Commit(4) };

        var rows = GraphLayout.Compute(commits, Id(3));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([1, 1, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenTwoBranchesAheadOfHead_WhenLayingOut_ThenTheFirstTakesLaneZeroAndTheOtherBendsInFromTheRight()
    {
        var commits = new[] { Commit(1, 3), Commit(2, 3), Commit(3, 4), Commit(4) };

        var rows = GraphLayout.Compute(commits, Id(3));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([0, 1, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenSameHistoryWithDetachedHead_WhenLayingOut_ThenTheOldLeftmostFreeLaneLayoutIsKept()
    {
        var commits = new[] { Commit(1, 4), Commit(2, 4), Commit(3, 4), Commit(4, 5), Commit(5) };

        var rows = GraphLayout.Compute(commits, headCommit: null);

        Assert.Equal([0, 1, 1, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenHeadMergingAFeatureBranch_WhenLayingOut_ThenTheFeatureLaneJoinsFromTheRight()
    {
        // 1 is another tip; HEAD 3 merges 6 (second parent) into 5; everything meets at 7.
        var commits = new[] { Commit(1, 5), Commit(3, 5, 6), Commit(6, 7), Commit(5, 7), Commit(7) };

        var rows = GraphLayout.Compute(commits, Id(3));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([1, 0, 2, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenHeadChainReachesACommitAlreadyWaitedForElsewhere_WhenLayingOut_ThenTheChainStillSitsInLaneZero()
    {
        // 1 (other tip) takes lane 1 for 3; HEAD 2 has first parent 3 too: 3 must still be in lane 0 and lane 1 joins it.
        var commits = new[] { Commit(1, 3), Commit(2, 3), Commit(3) };

        var rows = GraphLayout.Compute(commits, Id(2));

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        Assert.Equal([1, 0, 0], rows.Select(row => row.Lane));
    }

    [Fact]
    public void GivenHeadChainEnded_WhenLaterCommitsAppear_ThenLaneZeroIsFreeAgain()
    {
        // 1 is HEAD and a root; 2 and 3 are unrelated tips listed after it.
        var commits = new[] { Commit(1), Commit(2, 3), Commit(3) };

        var rows = GraphLayout.Compute(commits, Id(1));

        Assert.Equal([0, 0, 0], rows.Select(row => row.Lane));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenRandomHistoryAndAnyHead_WhenLayingOut_ThenFirstParentChainOfHeadIsLaneZeroAndNothingElseUsesItEarlier(int seed)
    {
        var commits = RandomHistory(seed, count: 400, window: 12);
        var head = Id(new Random(seed).Next(1, 200));
        var chain = FirstParentChain(commits, head);

        var rows = GraphLayout.Compute(commits, head);

        Assert.Empty(GraphTopologyValidator.Validate(commits, rows));
        var lastChainIndex = -1;
        for (var index = 0; index < rows.Count; index++)
        {
            if (chain.Contains(rows[index].Id))
            {
                Assert.Equal(0, rows[index].Lane);
                lastChainIndex = index;
            }
        }

        for (var index = 0; index < lastChainIndex; index++)
        {
            // Only a direct child of HEAD (a branch one commit ahead) may share lane 0 with the chain.
            var isDirectChild = commits.Any(commit => commit.Id == rows[index].Id && commit.Parents.Count > 0 && commit.Parents[0] == head);
            if (!chain.Contains(rows[index].Id) && !isDirectChild)
            {
                Assert.NotEqual(0, rows[index].Lane);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenAnyChunkingAndHead_WhenAppendingIncrementally_ThenRowsEqualTheFullLayout(int seed)
    {
        var commits = RandomHistory(seed, count: 300, window: 10);
        var head = Id(new Random(seed).Next(1, 150));
        var full = GraphLayout.Compute(commits, head);
        var random = new Random(seed);
        var builder = new GraphLayoutBuilder(head);

        var position = 0;
        while (position < commits.Count)
        {
            var size = Math.Min(random.Next(1, 40), commits.Count - position);
            builder.AppendRange(commits.Skip(position).Take(size));
            position += size;
            Assert.True(GraphRowComparer.AreEqual(full.Take(position).ToArray(), builder.Rows));
        }
    }

    [Fact]
    public void GivenDifferentHeads_WhenLayingOut_ThenTheLaneFingerprintDiffers()
    {
        var commits = new[] { Commit(1, 3), Commit(2, 3), Commit(3) };

        var forTwo = GraphLayout.Compute(commits, Id(2));
        var detached = GraphLayout.Compute(commits);

        Assert.NotEqual(forTwo[0].LaneFingerprint, detached[0].LaneFingerprint);
    }

    private static HashSet<ObjectId> FirstParentChain(IReadOnlyList<CommitInfo> commits, ObjectId head)
    {
        var byId = commits.ToDictionary(commit => commit.Id);
        var chain = new HashSet<ObjectId>();
        var current = head;
        while (byId.TryGetValue(current, out var commit))
        {
            chain.Add(commit.Id);
            if (commit.Parents.Count == 0)
            {
                break;
            }

            current = commit.Parents[0];
        }

        return chain;
    }
}
