using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class IncrementalGraphLoaderTests
{
    [Fact]
    public async Task GivenNewCommitOnTop_WhenReloading_ThenOldRowsAreReusedAndTheResultEqualsAFullRead()
    {
        using var repository = LongHistory(40);
        var previous = await ReadAllAsync(repository);
        repository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(repository, previous);

        Assert.True(reload.ReusedCount >= 38, "Reused " + reload.ReusedCount);
        await AssertEqualToFullReadAsync(repository, reload);
        Assert.Equal("new top", reload.Entries[0].Subject);
    }

    [Fact]
    public async Task GivenNothingChanged_WhenReloading_ThenEverythingBelowTheFirstRowIsReused()
    {
        using var repository = LongHistory(20);
        var previous = await ReadAllAsync(repository);

        var reload = await ReloadAsync(repository, previous);

        Assert.Equal(19, reload.ReusedCount);
        await AssertEqualToFullReadAsync(repository, reload);
    }

    [Fact]
    public async Task GivenNewBranchAndTagOnOldCommits_WhenReloading_ThenTheirRefsAppearOnReusedRows()
    {
        using var repository = LongHistory(30);
        var previous = await ReadAllAsync(repository);
        repository.Branch("topic", "HEAD~20").Tag("v1");
        repository.Git("tag", "old-tag", "HEAD~25");
        repository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(repository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(repository, reload);
        Assert.Contains(reload.Entries.Single(entry => entry.Subject == "commit 9").Decorations, decoration => decoration.Name == "topic");
        Assert.Contains(reload.Entries.Single(entry => entry.Subject == "commit 4").Decorations, decoration => decoration.Name == "old-tag");
    }

    [Fact]
    public async Task GivenRefsRemovedBelowTheTop_WhenReloading_ThenStaleDecorationsAreDropped()
    {
        using var repository = LongHistory(30);
        repository.Git("tag", "old-tag", "HEAD~25");
        repository.Branch("topic", "HEAD~20");
        var previous = await ReadAllAsync(repository);
        repository.Git("tag", "-d", "old-tag");
        repository.Git("branch", "-D", "topic");
        repository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(repository, previous);

        Assert.True(reload.ReusedCount > 20);
        await AssertEqualToFullReadAsync(repository, reload);
        Assert.All(reload.Entries.Skip(1), entry => Assert.Empty(entry.Decorations));
    }

    [Fact]
    public async Task GivenBranchDeletedWithCommitsBelowTheJoin_WhenReloading_ThenTheCountGuardFallsBackToAFullRead()
    {
        using var repository = LongHistory(20);
        repository.CheckoutNewBranch("topic").Commit("only on topic", ("topic.txt", "1")).Checkout("main");
        var previous = await ReadAllAsync(repository);
        repository.Git("branch", "-D", "topic");
        repository.Commit("new top", ("new.txt", "1"));

        var reload = await ReloadAsync(repository, previous);

        Assert.DoesNotContain(reload.Entries, entry => entry.Subject == "only on topic");
        await AssertEqualToFullReadAsync(repository, reload);
    }

    [Fact]
    public async Task GivenMergeOfASideBranch_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var repository = LongHistory(20);
        repository.CheckoutNewBranch("topic").Commit("topic 1", ("t1.txt", "1")).Commit("topic 2", ("t2.txt", "1")).Checkout("main");
        repository.Commit("main 1", ("m1.txt", "1"));
        var previous = await ReadAllAsync(repository);
        repository.Merge("topic");
        repository.Commit("after merge", ("a.txt", "1"));

        var reload = await ReloadAsync(repository, previous);

        await AssertEqualToFullReadAsync(repository, reload);
    }

    [Fact]
    public async Task GivenAmendedTopCommit_WhenReloading_ThenTheResultEqualsAFullRead()
    {
        using var repository = LongHistory(20);
        var previous = await ReadAllAsync(repository);
        repository.Git("commit", "-q", "--amend", "--allow-empty", "-m", "amended");

        var reload = await ReloadAsync(repository, previous);

        Assert.Equal("amended", reload.Entries[0].Subject);
        await AssertEqualToFullReadAsync(repository, reload);
    }

    [Fact]
    public async Task GivenEmptyPreviousGraph_WhenReloading_ThenEverythingIsRead()
    {
        using var repository = LongHistory(10);

        var reload = await ReloadAsync(repository, []);

        Assert.Equal(0, reload.ReusedCount);
        await AssertEqualToFullReadAsync(repository, reload);
    }

    private static TestRepository LongHistory(int count)
    {
        var repository = TestRepository.Create();
        repository.Commit("commit 0", ("a.txt", "0"));
        for (var number = 1; number < count; number++)
        {
            repository.Git("commit", "-q", "--allow-empty", "-m", "commit " + number);
        }

        return repository;
    }

    private static async Task<GraphEntry[]> ReadAllAsync(TestRepository repository)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<GraphReload> ReloadAsync(TestRepository repository, IReadOnlyList<GraphEntry> previous)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        return await IncrementalGraphLoader.ReloadAsync(repo, previous, CancellationToken.None);
    }

    private static async Task AssertEqualToFullReadAsync(TestRepository repository, GraphReload reload)
    {
        var expected = await ReadAllAsync(repository);

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
