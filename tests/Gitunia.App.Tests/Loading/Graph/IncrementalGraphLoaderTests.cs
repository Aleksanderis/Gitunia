using Gitunia.App.Loading.Graph;
using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Loading.Graph;

// The cases about reusing rows and the count guard run on a fake commit log. The cases below that compare a reload with a full read of real
// topology (merges, deleted branches, amended commits, ref changes on old commits) keep real temporary repositories: the topology and the
// decorations the real log reports are what they test.
public class IncrementalGraphLoaderTests
{
    [Fact]
    public async Task GivenNewCommitOnTop_WhenReloading_ThenOldRowsAreReusedAndTheResultEqualsAFullRead()
    {
        var commitLog = FakeCommitLogReader.Chain(40);
        var main = new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true);
        commitLog.Commits[0] = commitLog.Commits[0] with { Decorations = [main] };
        var previous = await ReadAllAsync(commitLog);

        // The branch moves up with the new commit, as a commit on main does.
        commitLog.Commits[0] = commitLog.Commits[0] with { Decorations = [] };
        commitLog.AddOnTop("new top", main);

        var reload = await ReloadAsync(commitLog, previous);

        Assert.True(reload.ReusedCount >= 38, $"Reused {reload.ReusedCount}");
        Assert.Equal("new top", reload.Entries[0].Subject);
        Assert.Equal(41, reload.Entries.Count);
        Assert.Equal((await ReadAllAsync(commitLog)).Select(entry => entry.Id), reload.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task GivenNothingChanged_WhenReloading_ThenEverythingBelowTheFirstRowIsReused()
    {
        var commitLog = FakeCommitLogReader.Chain(20);
        var previous = await ReadAllAsync(commitLog);

        var reload = await ReloadAsync(commitLog, previous);

        Assert.Equal(19, reload.ReusedCount);
        Assert.Equal(previous.Select(entry => entry.Id), reload.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task GivenCommitCountThatDisagreesWithTheReusedPart_WhenReloading_ThenTheCountGuardFallsBackToAFullRead()
    {
        var commitLog = FakeCommitLogReader.Chain(20);
        var previous = await ReadAllAsync(commitLog);
        commitLog.Commits.RemoveAt(commitLog.Commits.Count - 1);

        var reload = await ReloadAsync(commitLog, previous);

        Assert.Equal(0, reload.ReusedCount);
        Assert.Equal(19, reload.Entries.Count);
    }

    [Fact]
    public async Task GivenEmptyPreviousGraph_WhenReloading_ThenEverythingIsRead()
    {
        var commitLog = FakeCommitLogReader.Chain(10);

        var reload = await ReloadAsync(commitLog, []);

        Assert.Equal(0, reload.ReusedCount);
        Assert.Equal(10, reload.Entries.Count);
    }

    [Fact]
    public async Task GivenReadFailure_WhenReloading_ThenTheFailureReachesTheCaller()
    {
        var commitLog = FakeCommitLogReader.Chain(5);
        var previous = await ReadAllAsync(commitLog);
        commitLog.Failure = new GitException("boom");

        await Assert.ThrowsAsync<GitException>(() => ReloadAsync(commitLog, previous));
    }

    [Fact]
    public async Task GivenTheTipsCannotBeRead_WhenReloading_ThenTheWholeLogIsReadInstead()
    {
        var commitLog = FakeCommitLogReader.Chain(10);
        var main = new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true);
        commitLog.Commits[0] = commitLog.Commits[0] with { Decorations = [main] };
        var previous = await ReadAllAsync(commitLog);
        commitLog.Commits[0] = commitLog.Commits[0] with { Decorations = [] };
        commitLog.AddOnTop("new top", main);
        commitLog.TipFailure = new GitException("boom");

        var reload = await ReloadAsync(commitLog, previous);

        Assert.Equal(0, reload.ReusedCount);
        Assert.Equal(11, reload.Entries.Count);
    }

    [Fact]
    public async Task GivenNewTagsOnOldCommits_WhenReloading_ThenTheirRefsAppearOnReusedRows()
    {
        using var testRepository = LongHistory(30);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("tag", "other-tag", "HEAD~20");
        testRepository.Git("tag", "old-tag", "HEAD~25");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(testRepository, reload);
        var commitNine = reload.Entries.Single(entry => entry.Subject == "commit 9");
        Assert.Contains(commitNine.Decorations, decoration => decoration.Name == "other-tag");
        Assert.Contains(reload.Entries.Single(entry => entry.Subject == "commit 4").Decorations, decoration => decoration.Name == "old-tag");
    }

    [Fact]
    public async Task GivenNewBranchOnAnOldCommit_WhenReloading_ThenTheOldRowsAreReusedAndTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(30);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Branch("topic", "HEAD~20");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount > 25, $"Reused {reload.ReusedCount}");
        await AssertEqualToFullReadAsync(testRepository, reload);
        var commitNine = reload.Entries.Single(entry => entry.Subject == "commit 9");
        Assert.Contains(commitNine.Decorations, decoration => decoration.Name == "topic");
    }

    [Fact]
    public async Task GivenBranchesMovedOntoOldCommits_WhenReloading_ThenEverythingIsReusedAndEqualsAFullRead()
    {
        using var testRepository = LongHistory(30);
        testRepository.Branch("topic", "HEAD~20");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("branch", "-f", "topic", "HEAD~7");
        testRepository.Branch("other", "HEAD~12");

        var reload = await ReloadAsync(testRepository, previous);

        Assert.Equal(29, reload.ReusedCount);
        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.Contains(
            reload.Entries.Single(entry => entry.Subject == "commit 22").Decorations,
            decoration => decoration.Name == "topic");
    }

    [Fact]
    public async Task GivenBranchAddedOnAnotherBranchTip_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("zzz").Commit("zzz tip", new TestFile("z.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", new TestFile("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Branch("aaa", "zzz");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.Contains(
            reload.Entries.Single(entry => entry.Subject == "zzz tip").Decorations,
            decoration => decoration.Name == "aaa");
    }

    [Fact]
    public async Task GivenRefsRemovedBelowTheTop_WhenReloading_ThenStaleDecorationsAreDropped()
    {
        using var testRepository = LongHistory(30);
        testRepository.Git("tag", "old-tag", "HEAD~25");
        testRepository.Git("tag", "other-tag", "HEAD~20");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("tag", "-d", "old-tag");
        testRepository.Git("tag", "-d", "other-tag");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.All(reload.Entries.Skip(1), entry => Assert.Empty(entry.Decorations));
    }

    [Fact]
    public async Task GivenBranchDeletedWithCommitsBelowTheJoin_WhenReloading_ThenTheCountGuardFallsBackToAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("only on topic", new TestFile("topic.txt", "1")).Checkout("main");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("branch", "-D", "topic");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.DoesNotContain(reload.Entries, entry => entry.Subject == "only on topic");
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenBranchDeletedAndOneCommitAdded_WhenReloading_ThenTheGoneCommitIsNotKept()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("only on topic", new TestFile("topic.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", new TestFile("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("branch", "-D", "topic");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.DoesNotContain(reload.Entries, entry => entry.Subject == "only on topic");
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenMergeOfASideBranch_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("topic 1", new TestFile("t1.txt", "1")).Commit("topic 2", new TestFile("t2.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", new TestFile("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Merge("topic");
        testRepository.Commit("after merge", new TestFile("a.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenAmendedTopCommit_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("commit", "-q", "--amend", "--allow-empty", "-m", "amended");

        var reload = await ReloadAsync(testRepository, previous);

        Assert.Equal("amended", reload.Entries[0].Subject);
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenHeadMovedToAnotherBranch_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("topic 1", new TestFile("t1.txt", "1")).Commit("topic 2", new TestFile("t2.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", new TestFile("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Checkout("topic");

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenHeadMovedDownTheSameBranch_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("topic 1", new TestFile("t1.txt", "1")).Checkout("main");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("reset", "-q", "--hard", "HEAD~5");

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenCurrentBranchSwitchedToDetachedHead_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("topic 1", new TestFile("t1.txt", "1")).Checkout("main");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("checkout", "-q", "--detach", "topic");

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenBranchesOnConsecutiveCommitsOfMain_WhenReading_ThenTheLineStaysOneStraightLane()
    {
        using var testRepository = LongHistory(6);
        testRepository.Branch("first", "HEAD~1");
        testRepository.Branch("second", "HEAD~3");

        var entries = await ReadAllAsync(testRepository);

        Assert.All(entries, entry => Assert.Equal(0, entry.Row.Lane));
        Assert.Single(entries.Select(entry => entry.Row.NodeColor).Distinct());
    }

    [Fact]
    public async Task GivenTheDemoRepository_WhenReading_ThenTheLeftmostLaneIsStraightAndBranchesKeepOneLane()
    {
        using var testRepository = DemoRepository();

        var entries = await ReadAllAsync(testRepository);

        AssertDemoLayout(entries);
    }

    [Fact]
    public async Task GivenTheDemoRepository_WhenAMainCommitIsAddedAndTheGraphReloaded_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = DemoRepository();
        var previous = await ReadAllAsync(testRepository);
        testRepository.Commit("Add changelog", new TestFile("changelog.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
        AssertDemoLayout([.. reload.Entries.Skip(1)]);
    }

    [Fact]
    public async Task GivenBranchesOnCommitsOfOneChain_WhenReloadingAfterANewCommit_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(30);
        testRepository.Branch("first", "HEAD~1");
        testRepository.Branch("second", "HEAD~10");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenCurrentBranchWithTheOldestTip_WhenReading_ThenTheNewestTipIsLeftmostAndHeadIsNot()
    {
        using var testRepository = LongHistory(3);
        testRepository.Checkout("main~1").CheckoutNewBranch("older").Commit("older tip", new TestFile("o.txt", "1"));
        testRepository.Checkout("main~1")
            .CheckoutNewBranch("newest")
            .Commit("newest tip", new TestFile("n.txt", "1"))
            .Checkout("main");

        var entries = await ReadAllAsync(testRepository);

        // Main (HEAD) is the oldest tip: it gets no lane of its own, the newest branch is lane 0.
        var newest = entries.Single(entry => entry.Subject == "newest tip");
        var older = entries.Single(entry => entry.Subject == "older tip");
        var head = entries.Single(entry => entry.Id.ToString() == testRepository.Head);
        Assert.Equal(0, newest.Row.Lane);
        Assert.Equal(1, older.Row.Lane);
        Assert.Equal(2, head.Row.Lane);
    }

    // The history of the owner's demo: a main line with release, develop and hotfix on consecutive commits, three
    // feature branches forked from "Add cli" and committed round-robin, and a merged topic branch.
    private static TestRepository DemoRepository()
    {
        var repository = TestRepository.Create();
        repository.Commit("Initial commit", new TestFile("f.txt", "0"));
        repository.Commit("Add parser", new TestFile("f.txt", "1")).Branch("release");
        repository.Commit("Add lexer", new TestFile("f.txt", "2")).Branch("develop");
        repository.Commit("Add formatter", new TestFile("f.txt", "3")).Branch("hotfix");
        repository.Commit("Add cli", new TestFile("f.txt", "4"));
        foreach (var feature in DemoFeatures)
        {
            repository.Branch(feature);
        }

        for (var step = 1; step <= 3; step++)
        {
            foreach (var feature in DemoFeatures)
            {
                repository.Checkout(feature)
                    .Commit($"{feature} step {step}", new TestFile($"{feature}{step}.txt", "1"));
            }
        }

        repository.Checkout("main").CheckoutNewBranch("merged-topic").Commit("Topic work", new TestFile("t.txt", "1"));
        repository.Checkout("main").Merge("merged-topic");
        repository.Commit("Add docs", new TestFile("f.txt", "5"));

        return repository;
    }

    private static void AssertDemoLayout(GraphEntry[] entries)
    {
        var main = DemoMainSubjects.Select(subject => Find(entries, subject)).ToArray();
        Assert.All(main, entry => Assert.Equal(0, entry.Row.Lane));
        Assert.Single(main.Select(entry => entry.Row.NodeColor).Distinct());

        // Newest tip nearest main: the merged topic, then feat-c, feat-b, feat-a.
        Assert.Equal(1, Find(entries, "Topic work").Row.Lane);
        var expectedLane = 2;
        var colours = new List<int> { main[0].Row.NodeColor, Find(entries, "Topic work").Row.NodeColor };
        foreach (var feature in DemoFeaturesNewestFirst)
        {
            var lanes = Enumerable.Range(1, 3).Select(step => Find(entries, $"{feature} step {step}")).ToArray();
            Assert.All(lanes, entry => Assert.Equal(expectedLane, entry.Row.Lane));
            Assert.Single(lanes.Select(entry => entry.Row.NodeColor).Distinct());
            colours.Add(lanes[0].Row.NodeColor);
            expectedLane++;
        }

        Assert.Equal(colours.Count, colours.Distinct().Count());

        // Every tip is on its own row and the branch lanes join main at the fork commit.
        var fork = Find(entries, "Add cli");
        Assert.Equal(
            [0, 1, 2, 3, 4],
            fork.Row.Edges.Where(edge => edge.Kind == GraphEdgeKind.Incoming).Select(edge => edge.FromLane));
    }

    private static GraphEntry Find(GraphEntry[] entries, string subject) =>
        entries.Single(entry => entry.Subject == subject);

    private static readonly string[] DemoMainSubjects =
    [
        "Add docs", "Merge merged-topic", "Add cli", "Add formatter", "Add lexer", "Add parser", "Initial commit",
    ];

    private static readonly string[] DemoFeaturesNewestFirst = ["feat-c", "feat-b", "feat-a"];

    private static readonly string[] DemoFeatures = ["feat-a", "feat-b", "feat-c"];

    private static async Task<GraphEntry[]> ReadAllAsync(FakeCommitLogReader commitLog)
    {
        var entries = new List<GraphEntry>();
        await foreach (var batch in new GraphLoader(commitLog).StreamAsync(FakeStatusReader.LocationOf("incremental"), CommitOrder.ByDate, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    [Fact]
    public async Task GivenCappedReloadOverTheLimit_WhenReloading_ThenOnlyTheNewestRowsComeAndTheTotalIsReported()
    {
        var commitLog = FakeCommitLogReader.Chain(10);
        var loader = new IncrementalGraphLoader(commitLog, new GraphLoader(commitLog, 4));

        var location = FakeStatusReader.LocationOf("incremental");
        var reload = await loader.ReloadAsync(location, [], true, CancellationToken.None);

        Assert.Equal(4, reload.Entries.Count);
        Assert.Equal(10, reload.TruncatedTotal);
        Assert.Equal(0, reload.ReusedCount);
    }

    [Fact]
    public async Task GivenUncappedReloadOfALogLongerThanTheLimit_WhenReloading_ThenEverythingComesAndNothingIsCut()
    {
        var commitLog = FakeCommitLogReader.Chain(10);
        var loader = new IncrementalGraphLoader(commitLog, new GraphLoader(commitLog, 4));

        var location = FakeStatusReader.LocationOf("incremental");
        var reload = await loader.ReloadAsync(location, [], false, CancellationToken.None);

        Assert.Equal(10, reload.Entries.Count);
        Assert.Null(reload.TruncatedTotal);
    }

    private static Task<GraphReload> ReloadAsync(FakeCommitLogReader commitLog, IReadOnlyList<GraphEntry> previous) =>
        new IncrementalGraphLoader(commitLog, new GraphLoader(commitLog))
            .ReloadAsync(FakeStatusReader.LocationOf("incremental"), previous, false, CancellationToken.None);

    private static TestRepository LongHistory(int count)
    {
        var testRepository = TestRepository.Create();
        testRepository.Commit("commit 0", new TestFile("a.txt", "0"));
        for (var number = 1; number < count; number++)
        {
            testRepository.Git("commit", "-q", "--allow-empty", "-m", $"commit {number}");
        }

        return testRepository;
    }

    private static async Task<GraphEntry[]> ReadAllAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in TestGit.Services.GraphLoader.StreamAsync(location, CommitOrder.ByDate, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<GraphReload> ReloadAsync(TestRepository testRepository, IReadOnlyList<GraphEntry> previous)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        return await TestGit.Services.IncrementalGraphLoader
            .ReloadAsync(location, previous, false, CancellationToken.None);
    }

    private static async Task AssertEqualToFullReadAsync(TestRepository testRepository, GraphReload reload)
    {
        var expected = await ReadAllAsync(testRepository);

        Assert.Equal(expected.Length, reload.Entries.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            var left = expected[index];
            var right = reload.Entries[index];
            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.Subject, right.Subject);
            Assert.Equal(left.AuthorName, right.AuthorName);
            Assert.Equal(left.AuthorTime, right.AuthorTime);
            Assert.Equal(left.Decorations, right.Decorations);
            Assert.Equal(left.Row.Lane, right.Row.Lane);
            Assert.Equal(left.Row.LaneCount, right.Row.LaneCount);
            Assert.Equal(left.Row.LaneFingerprint, right.Row.LaneFingerprint);
            Assert.Equal(left.Row.Edges, right.Row.Edges);
            Assert.Equal(left.Row.NodeColor, right.Row.NodeColor);
            Assert.Equal(left.Row.ColorChosenByName, right.Row.ColorChosenByName);
        }
    }
}
