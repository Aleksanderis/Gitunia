using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

// The cases about reusing rows and the count guard run on a fake commit log. The cases below that compare a reload with a full read of real
// topology (merges, deleted branches, amended commits, ref changes on old commits) keep real temporary repositories: the topology and the
// decorations the real log reports are what they test.
public class IncrementalGraphLoaderTests
{
    [Fact]
    public async Task GivenNewCommitOnTop_WhenReloading_ThenOldRowsAreReusedAndTheResultEqualsAFullRead()
    {
        var commitLog = FakeCommitLogReader.Chain(40);
        var previous = await ReadAllAsync(commitLog);
        commitLog.AddOnTop("new top");

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
    public async Task GivenNewBranchAndTagOnOldCommits_WhenReloading_ThenTheirRefsAppearOnReusedRows()
    {
        using var testRepository = LongHistory(30);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Branch("topic", "HEAD~20").Tag("v1");
        testRepository.Git("tag", "old-tag", "HEAD~25");
        testRepository.Commit("new top", new TestFile("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.Contains(reload.Entries.Single(entry => entry.Subject == "commit 9").Decorations, decoration => decoration.Name == "topic");
        Assert.Contains(reload.Entries.Single(entry => entry.Subject == "commit 4").Decorations, decoration => decoration.Name == "old-tag");
    }

    [Fact]
    public async Task GivenRefsRemovedBelowTheTop_WhenReloading_ThenStaleDecorationsAreDropped()
    {
        using var testRepository = LongHistory(30);
        testRepository.Git("tag", "old-tag", "HEAD~25");
        testRepository.Branch("topic", "HEAD~20");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("tag", "-d", "old-tag");
        testRepository.Git("branch", "-D", "topic");
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

    private static async Task<GraphEntry[]> ReadAllAsync(FakeCommitLogReader commitLog)
    {
        var entries = new List<GraphEntry>();
        await foreach (var batch in new GraphLoader(commitLog).StreamAsync(FakeStatusReader.LocationOf("incremental"), CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static Task<GraphReload> ReloadAsync(FakeCommitLogReader commitLog, IReadOnlyList<GraphEntry> previous) =>
        new IncrementalGraphLoader(commitLog, new GraphLoader(commitLog)).ReloadAsync(FakeStatusReader.LocationOf("incremental"), previous, CancellationToken.None);

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
        await foreach (var batch in TestGit.Services.GraphLoader.StreamAsync(location, CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<GraphReload> ReloadAsync(TestRepository testRepository, IReadOnlyList<GraphEntry> previous)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        return await TestGit.Services.IncrementalGraphLoader.ReloadAsync(location, previous, CancellationToken.None);
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
        }
    }
}
