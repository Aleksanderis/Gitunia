using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Xunit;
using static Gitunia.Core.Tests.Graph.GraphTestData;

namespace Gitunia.Core.Tests.Graph;

public class GraphSplicerTests
{
    private static readonly IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> NoDecorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>();

    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 40)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenNewCommitOnTopOfTheNewestCommit_WhenSplicing_ThenResultEqualsFullLayoutAndMostRowsAreReused(int seed)
    {
        var old = RandomHistory(seed, count: 400, window: 12);
        var updated = new List<CommitInfo> { Commit(10_000, 1) };
        updated.AddRange(old);

        var (result, joinIndex) = Reload(old, updated);

        Assert.True(joinIndex >= 0);
        Assert.True(400 - joinIndex - 1 > 300, $"Expected most rows below the join to be reused, joined at {joinIndex}");
        AssertEqualToFullLayout(updated, result);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void GivenNewTipOnAnOlderCommit_WhenSplicingWhereverItJoins_ThenResultEqualsFullLayout(int seed)
    {
        var old = RandomHistory(seed, count: 300, window: 10);
        var updated = new List<CommitInfo> { Commit(10_000, 150) };
        updated.AddRange(old);

        var (result, joinIndex) = Reload(old, updated);

        Assert.True(joinIndex >= 0);
        AssertEqualToFullLayout(updated, result);
    }

    [Fact]
    public void GivenUnchangedLog_WhenObservingTheFirstRow_ThenItJoinsImmediately()
    {
        var old = RandomHistory(3, count: 50, window: 6);

        var (result, joinIndex) = Reload(old, old);

        Assert.Equal(0, joinIndex);
        AssertEqualToFullLayout(old, result);
    }

    [Fact]
    public void GivenSameCommitWithDifferentOpenLanes_WhenObserving_ThenItDoesNotJoin()
    {
        // 2 is the first parent of 5 in the old log but also of a second tip (6) in the new one: same commit, other lanes below it.
        var old = new[] { Commit(5, 2), Commit(3, 2), Commit(2, 1), Commit(1) };
        var updated = new[] { Commit(6, 2), Commit(5, 2), Commit(3, 2), Commit(2, 1), Commit(1) };
        var splicer = new GraphSplicer(Entries(old));
        var builder = new GraphLayoutBuilder();

        var joins = updated.Select(commit => splicer.Observe(ToEntry(commit, builder.Append(commit)))).ToArray();

        Assert.Equal(-1, joins[0]);
        Assert.Equal(-1, joins[1]);
    }

    [Fact]
    public void GivenRowsThatSwappedOrder_WhenObserving_ThenTheLowerRowDoesNotJoin()
    {
        var old = new[] { Commit(4, 3), Commit(3, 2), Commit(5, 2), Commit(2) };
        var splicer = new GraphSplicer(Entries(old));
        var builder = new GraphLayoutBuilder();
        var reordered = new[] { Commit(5, 2), Commit(4, 3), Commit(3, 2), Commit(2) };

        var joins = reordered.Select(commit => splicer.Observe(ToEntry(commit, builder.Append(commit)))).ToArray();

        // 5 sat below 3 before; after seeing it first, rows from the old head cannot join on a changed order.
        Assert.All(joins.Take(2), join => Assert.Equal(-1, join));
    }

    [Fact]
    public void GivenMovedRefs_WhenSplicing_ThenTailDecorationsFollowTheNewRefs()
    {
        var oldTag = new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false);
        var newBranch = new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false);
        var old = new[] { Commit(5, 4), Commit(4, 3), Commit(3, 2) with { Decorations = [oldTag] }, Commit(2, 1), Commit(1) };
        var updated = new[] { Commit(6, 5), Commit(5, 4), Commit(4, 3), Commit(3, 2), Commit(2, 1), Commit(1) };
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>> { [Id(1)] = [newBranch] };

        var (result, joinIndex) = Reload(old, updated, decorations);

        Assert.Equal(1, joinIndex);
        Assert.Equal(6, result.Count);
        Assert.Empty(result[3].Decorations);
        Assert.Empty(result[4].Decorations);
        Assert.Equal([newBranch], result[5].Decorations);
    }

    [Fact]
    public void GivenReusedTipThatLostItsOnlyRef_WhenSplicing_ThenItIsReportedAsPossiblyGone()
    {
        var branch = new CommitDecoration("refs/heads/topic", "topic", RefKind.LocalBranch, false);
        var old = new[] { Commit(4, 3), Commit(3, 2), Commit(9, 2) with { Decorations = [branch] }, Commit(2) };
        var updated = new[] { Commit(5, 4), Commit(4, 3), Commit(3, 2), Commit(2) };
        var splicer = new GraphSplicer(Entries(old));
        var builder = new GraphLayoutBuilder();
        var rows = new List<GraphEntry>();
        var joinIndex = -1;
        foreach (var commit in updated)
        {
            rows.Add(ToEntry(commit, builder.Append(commit)));
            joinIndex = splicer.Observe(rows[^1]);
            if (joinIndex >= 0)
            {
                break;
            }
        }

        Assert.True(joinIndex >= 0);
        Assert.Null(splicer.Splice(rows, joinIndex, NoDecorations));
    }

    private static ReloadOutcome Reload(
        IReadOnlyList<CommitInfo> old,
        IReadOnlyList<CommitInfo> updated,
        IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>>? decorations = null)
    {
        var splicer = new GraphSplicer(Entries(old));
        var builder = new GraphLayoutBuilder();
        var rows = new List<GraphEntry>();
        foreach (var commit in updated)
        {
            rows.Add(ToEntry(commit, builder.Append(commit)));
            var joinIndex = splicer.Observe(rows[^1]);
            if (joinIndex >= 0)
            {
                return new ReloadOutcome(splicer.Splice(rows, joinIndex, decorations ?? NoDecorations)!, joinIndex);
            }
        }

        return new ReloadOutcome(rows, -1);
    }

    private static List<GraphEntry> Entries(IReadOnlyList<CommitInfo> commits)
    {
        var builder = new GraphLayoutBuilder();
        return [.. commits.Select(commit => ToEntry(commit, builder.Append(commit)))];
    }

    private static GraphEntry ToEntry(CommitInfo commit, GraphRow row) =>
        new(commit.Id, commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, row);

    private static void AssertEqualToFullLayout(IReadOnlyList<CommitInfo> commits, IReadOnlyList<GraphEntry> result)
    {
        var expected = GraphLayout.Compute(commits);
        Assert.Equal([.. commits.Select(commit => commit.Id)], [.. result.Select(entry => entry.Id)]);
        Assert.True(GraphRowComparer.AreEqual(expected, [.. result.Select(entry => entry.Row)]));
    }
}
