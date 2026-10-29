using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

// The merged-history case keeps a real repository: the lanes and the decorations come from real topology and real refs. The batching,
// limit, cancellation and failure cases only need a stream of commits and run on a fake commit log.
public class GraphLoaderTests
{
    [Fact]
    public async Task GivenMergedFeatureBranchHistory_WhenStreaming_ThenRowsMatchTheBatchLayoutAndRefsAreAttached()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Commit("root", new TestFile("a.txt", "1"));
        testRepository.CheckoutNewBranch("topic").Commit("on topic", new TestFile("b.txt", "1"));
        testRepository.Checkout("main").Commit("on main", new TestFile("c.txt", "1"));
        testRepository.Merge("topic").Tag("v1");

        var entries = (await LoadAsync(testRepository)).SelectMany(batch => batch).ToList();

        Assert.Equal("Merge topic", entries[0].Subject);
        Assert.Equal("root", entries[3].Subject);
        Assert.Equal(["on main", "on topic"], [.. entries.Skip(1).Take(2).Select(entry => entry.Subject).Order()]);
        Assert.Equal(entries.Select(entry => entry.Id.ToString()).ToArray(), entries.Select(entry => entry.Row.Id.ToString()).ToArray());
        Assert.Equal(2, entries[0].Row.Edges.Count(edge => edge.Kind == GraphEdgeKind.Outgoing));
        Assert.Contains(entries[0].Decorations, decoration => decoration.Name == "main" && decoration.IsCurrent);
        Assert.Contains(entries[0].Decorations, decoration => decoration.Name == "v1");
        Assert.Contains(entries.Single(entry => entry.Subject == "on topic").Decorations, decoration => decoration.Name == "topic");
    }

    [Fact]
    public async Task GivenTwoBranchesAndTheirRemotes_WhenStreaming_ThenEachBranchHasOneColourAndLocalAndRemoteShareIt()
    {
        var commitLog = new FakeCommitLogReader();
        var root = commitLog.AddOnTop("root");
        var alpha = commitLog.AddOnTop("alpha", Branch("alpha"));
        commitLog.Commits.Insert(0, Tip(10, "beta", root, Branch("beta")));
        commitLog.Commits.Insert(0, Tip(11, "origin alpha", alpha, Remote("origin/alpha")));

        var entries = (await LoadAsync(commitLog)).SelectMany(batch => batch).ToList();

        var originAlpha = entries.Single(entry => entry.Subject == "origin alpha").Row.NodeColor;
        var localAlpha = entries.Single(entry => entry.Subject == "alpha").Row.NodeColor;
        var beta = entries.Single(entry => entry.Subject == "beta").Row.NodeColor;
        Assert.Equal(BranchColors.PreferredSlot("alpha"), originAlpha);
        Assert.Equal(originAlpha, localAlpha);
        Assert.NotEqual(originAlpha, beta);
    }

    [Fact]
    public async Task GivenTheLogReadFails_WhenStreaming_ThenTheTipReadIsCancelled()
    {
        var commitLog = FakeCommitLogReader.Chain(3);
        commitLog.Failure = new GitException("boom");

        await Assert.ThrowsAsync<GitException>(() => LoadAsync(commitLog));

        Assert.True(commitLog.LastTipToken.IsCancellationRequested);
    }

    [Fact]
    public async Task GivenAShortPalette_WhenStreaming_ThenTheColoursStayWithinIt()
    {
        var commitLog = new FakeCommitLogReader();
        var root = commitLog.AddOnTop("root");
        for (var number = 10; number < 16; number++)
        {
            commitLog.Commits.Insert(0, Tip(number, $"tip {number}", root, Branch($"branch-{number}")));
        }

        var loader = new GraphLoader(commitLog, colorCount: () => 3);
        var entries = new List<GraphEntry>();
        await foreach (var batch in loader.StreamAsync(
            FakeStatusReader.LocationOf("graph"), CommitOrder.ByDate, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        Assert.All(entries, entry => Assert.InRange(entry.Row.NodeColor, 0, 2));
    }

    [Fact]
    public async Task GivenAllCommitsWithOneTimestampAndMainListedLast_WhenStreaming_ThenMainIsStillOneStraightLane()
    {
        // The demo history as git lists it when every commit has the same timestamp: the feature tips first, main last.
        var commitLog = new FakeCommitLogReader();
        var main = new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true);
        commitLog.Commits.AddRange(
        [
            Node(4, "feat-a step 3", [7], Branch("feat-a")),
            Node(5, "feat-b step 3", [8], Branch("feat-b")),
            Node(1, "Add docs", [2], main),
            Node(2, "Merge merged-topic", [11, 3]),
            Node(3, "Topic work", [11], Branch("merged-topic")),
            Node(7, "feat-a step 2", [9]),
            Node(8, "feat-b step 2", [10]),
            Node(9, "feat-a step 1", [11]),
            Node(10, "feat-b step 1", [11]),
            Node(11, "Add cli", [12]),
            Node(12, "Add formatter", [13], Branch("hotfix")),
            Node(13, "Initial commit", []),
        ]);

        var entries = (await LoadAsync(commitLog)).SelectMany(batch => batch).ToList();

        var mainSubjects = new[] { "Add docs", "Merge merged-topic", "Add cli", "Add formatter", "Initial commit" };
        var mainLane = entries.Single(entry => entry.Subject == "Add docs").Row.Lane;
        Assert.All(
            entries.Where(entry => mainSubjects.Contains(entry.Subject)),
            entry => Assert.Equal(mainLane, entry.Row.Lane));
        Assert.Equal(2, entries.Single(entry => entry.Subject == "Add docs").Row.Lane);
        var colours = entries.Where(entry => mainSubjects.Contains(entry.Subject)).Select(entry => entry.Row.NodeColor);
        Assert.Single(colours.Distinct());
    }

    [Fact]
    public async Task GivenTheTipsCannotBeRead_WhenStreaming_ThenTheGraphStillLoads()
    {
        var commitLog = FakeCommitLogReader.Chain(5);
        commitLog.TipFailure = new GitException("boom");

        var entries = (await LoadAsync(commitLog)).SelectMany(batch => batch).ToList();

        Assert.Equal(5, entries.Count);
    }

    [Fact]
    public async Task GivenMoreCommitsThanTheFirstBatch_WhenStreaming_ThenFirstBatchIsSmallAndNothingIsLost()
    {
        var batches = await LoadAsync(FakeCommitLogReader.Chain(200));

        Assert.True(batches.Count >= 2);
        Assert.True(batches[0].Count <= 150);
        Assert.Equal(200, batches.Sum(batch => batch.Count));
    }

    [Fact]
    public async Task GivenMaximum_WhenStreaming_ThenStopsAfterThatManyCommits()
    {
        var entries = (await LoadAsync(FakeCommitLogReader.Chain(3), maximumCommits: 2)).SelectMany(batch => batch).ToList();

        Assert.Equal(["commit 3", "commit 2"], [.. entries.Select(entry => entry.Subject)]);
    }

    [Fact]
    public async Task GivenLogOverTheSoftLimit_WhenStreamingCapped_ThenOnlyTheNewestRowsComeAndTheCutHoldsTheTotal()
    {
        var cut = new GraphCut();

        var batches = await LoadCappedAsync(FakeCommitLogReader.Chain(10), 4, cut);

        var subjects = batches.SelectMany(batch => batch).Select(entry => entry.Subject).ToList();
        Assert.Equal(["commit 10", "commit 9", "commit 8", "commit 7"], subjects);
        Assert.Equal(10, cut.TruncatedTotal);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(0)]
    public async Task GivenLogWithinTheSoftLimit_WhenStreamingCapped_ThenEverythingComesAndNothingIsCut(int commits)
    {
        var cut = new GraphCut();

        var batches = await LoadCappedAsync(FakeCommitLogReader.Chain(commits), 4, cut);

        Assert.Equal(commits, batches.Sum(batch => batch.Count));
        Assert.Null(cut.TruncatedTotal);
    }

    [Fact]
    public async Task GivenRealHistoryLongerThanTheSoftLimit_WhenStreamingCapped_ThenTheRowsAreThePrefixOfAFullRead()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Commit("commit 0", new TestFile("a.txt", "0"));
        for (var number = 1; number < 12; number++)
        {
            testRepository.Git("commit", "-q", "--allow-empty", "-m", $"commit {number}");
        }

        var path = testRepository.WorkingTreePath;
        var location = await TestGit.Services.Discovery.DiscoverAsync(path, CancellationToken.None);
        var cut = new GraphCut();
        var capped = await LoadCappedAsync(new GraphLoader(TestGit.Services.CommitLog, 5), location, cut);
        var full = (await LoadAsync(testRepository)).SelectMany(batch => batch).ToList();

        Assert.Equal(12, cut.TruncatedTotal);
        var cappedIds = capped.SelectMany(batch => batch).Select(entry => entry.Id);
        Assert.Equal(full.Take(5).Select(entry => entry.Id), cappedIds);
    }

    [Fact]
    public async Task GivenCancelledToken_WhenStreaming_ThenItStopsWithCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in new GraphLoader(FakeCommitLogReader.Chain(1)).StreamAsync(FakeStatusReader.LocationOf("graph"), CommitOrder.Topological, null, cancellation.Token))
            {
                Assert.NotEmpty(batch);
            }
        });
    }

    [Fact]
    public async Task GivenReadFailure_WhenStreaming_ThenTheFailureReachesTheCaller()
    {
        var commitLog = FakeCommitLogReader.Chain(1);
        commitLog.Failure = new GitException("boom");

        await Assert.ThrowsAsync<GitException>(() => LoadAsync(commitLog));
    }

    private static CommitInfo Node(int number, string subject, int[] parents, params CommitDecoration[] decorations) =>
        new(
            FakeObjectIds.Of(number),
            [.. parents.Select(FakeObjectIds.Of)],
            "Author",
            "author@example.com",
            DateTimeOffset.UnixEpoch,
            subject,
            decorations);

    private static CommitDecoration Branch(string name) =>
        new($"refs/heads/{name}", name, RefKind.LocalBranch, false);

    private static CommitDecoration Remote(string name) =>
        new($"refs/remotes/{name}", name, RefKind.RemoteBranch, false);

    private static CommitInfo Tip(int number, string subject, CommitInfo parent, CommitDecoration decoration) =>
        new(
            FakeObjectIds.Of(number),
            [parent.Id],
            "Author",
            "author@example.com",
            DateTimeOffset.UnixEpoch,
            subject,
            [decoration]);

    private static Task<List<IReadOnlyList<GraphEntry>>> LoadCappedAsync(
        FakeCommitLogReader commitLog,
        int softLimit,
        GraphCut cut) =>
        LoadCappedAsync(new GraphLoader(commitLog, softLimit), FakeStatusReader.LocationOf("graph"), cut);

    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadCappedAsync(
        GraphLoader loader,
        RepositoryLocation location,
        GraphCut cut)
    {
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in loader.StreamCappedAsync(location, cut, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }

    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(TestRepository testRepository, int? maximumCommits = null)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in TestGit.Services.GraphLoader.StreamAsync(location, CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }

    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(FakeCommitLogReader commitLog, int? maximumCommits = null)
    {
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in new GraphLoader(commitLog).StreamAsync(FakeStatusReader.LocationOf("graph"), CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }
}
