using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

public class IncrementalGraphLoaderTests
{
    [Fact]
    public async Task GivenNewCommitOnTop_WhenReloading_ThenOldRowsAreReusedAndTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(40);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount >= 38, "Reused " + reload.ReusedCount);
        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.Equal("new top", reload.Entries[0].Subject);
    }

    [Fact]
    public async Task GivenNothingChanged_WhenReloading_ThenEverythingBelowTheFirstRowIsReused()
    {
        using var testRepository = LongHistory(20);
        var previous = await ReadAllAsync(testRepository);

        var reload = await ReloadAsync(testRepository, previous);

        Assert.Equal(19, reload.ReusedCount);
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenNewBranchAndTagOnOldCommits_WhenReloading_ThenTheirRefsAppearOnReusedRows()
    {
        using var testRepository = LongHistory(30);
        var previous = await ReadAllAsync(testRepository);
        testRepository.Branch("topic", "HEAD~20").Tag("v1");
        testRepository.Git("tag", "old-tag", "HEAD~25");
        testRepository.Commit("new top", ("new.txt", "1"));

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
        testRepository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(testRepository, reload);
        Assert.All(reload.Entries.Skip(1), entry => Assert.Empty(entry.Decorations));
    }

    [Fact]
    public async Task GivenBranchDeletedWithCommitsBelowTheJoin_WhenReloading_ThenTheCountGuardFallsBackToAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("only on topic", ("topic.txt", "1")).Checkout("main");
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("branch", "-D", "topic");
        testRepository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.DoesNotContain(reload.Entries, entry => entry.Subject == "only on topic");
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenBranchDeletedAndOneCommitAdded_WhenReloading_ThenTheGoneCommitIsNotKept()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("only on topic", ("topic.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", ("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Git("branch", "-D", "topic");
        testRepository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(testRepository, previous);

        Assert.DoesNotContain(reload.Entries, entry => entry.Subject == "only on topic");
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    [Fact]
    public async Task GivenMergeOfASideBranch_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var testRepository = LongHistory(20);
        testRepository.CheckoutNewBranch("topic").Commit("topic 1", ("t1.txt", "1")).Commit("topic 2", ("t2.txt", "1")).Checkout("main");
        testRepository.Commit("main 1", ("m1.txt", "1"));
        var previous = await ReadAllAsync(testRepository);
        testRepository.Merge("topic");
        testRepository.Commit("after merge", ("a.txt", "1"));

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
    public async Task GivenEmptyPreviousGraph_WhenReloading_ThenEverythingIsRead()
    {
        using var testRepository = LongHistory(10);

        var reload = await ReloadAsync(testRepository, []);

        Assert.Equal(0, reload.ReusedCount);
        await AssertEqualToFullReadAsync(testRepository, reload);
    }

    private static TestRepository LongHistory(int count)
    {
        var testRepository = TestRepository.Create();
        testRepository.Commit("commit 0", ("a.txt", "0"));
        for (var number = 1; number < count; number++)
        {
            testRepository.Git("commit", "-q", "--allow-empty", "-m", "commit " + number);
        }

        return testRepository;
    }

    private static async Task<GraphEntry[]> ReadAllAsync(TestRepository testRepository)
    {
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in GraphLoader.StreamAsync(repository, CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<GraphReload> ReloadAsync(TestRepository testRepository, IReadOnlyList<GraphEntry> previous)
    {
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.Path, CancellationToken.None);
        return await IncrementalGraphLoader.ReloadAsync(repository, previous, CancellationToken.None);
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
