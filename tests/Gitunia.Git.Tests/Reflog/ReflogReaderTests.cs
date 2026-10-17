using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Reflog;
using Gitunia.Git.Reflog.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Reflog;

public class ReflogReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenCommitAmendResetAndCheckout_WhenReadingHead_ThenEachActionIsListedNewestFirst()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        repository.Git("commit", "--amend", "-m", "two amended");
        repository.Git("reset", "--hard", first);
        repository.Branch("topic");
        repository.Checkout("topic");

        var result = await ReadAsync(repository, "HEAD");

        var actions = result.Items.Select(entry => entry.Action).ToList();
        Assert.Equal(["checkout", "reset", "commit (amend)", "commit", "commit (initial)"], actions);
        Assert.Equal(Enumerable.Range(0, 5), result.Items.Select(entry => entry.Index));
        Assert.Equal(first, result.Items[0].Hash);
        Assert.Equal("moving from main to topic", result.Items[0].Message);
        Assert.Equal("two amended", result.Items[2].Message);
        Assert.All(result.Items, entry => Assert.NotEqual(string.Empty, entry.RelativeDate));
    }

    [Fact]
    public async Task GivenACommitLostByResetHard_WhenReadingHead_ThenItsHashIsStillListed()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));
        var lost = repository.Head;
        repository.Git("reset", "--hard", "HEAD~1");

        var result = await ReadAsync(repository, "HEAD");

        Assert.Contains(result.Items, entry => entry.Hash == lost && entry.Message == "two");
    }

    [Fact]
    public async Task GivenABranch_WhenReadingItsReflog_ThenOnlyThatBranchsEntriesAreListed()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        repository.CheckoutNewBranch("topic");
        repository.Commit("on topic", new TestFile("b.txt", "b"));

        var result = await ReadAsync(repository, "refs/heads/topic");

        Assert.Equal(["commit", "branch"], result.Items.Select(entry => entry.Action));
    }

    [Fact]
    public async Task GivenALimit_WhenReading_ThenAtMostThatManyEntriesAreReturned()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"))
            .Commit("three", new TestFile("a.txt", "3"));

        var result = await ReadAsync(repository, "HEAD", limit: 2);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("three", result.Items[0].Message);
    }

    [Fact]
    public async Task GivenAnUnknownBranch_WhenReading_ThenTheTypedExceptionIsThrown()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReadAsync(repository, "refs/heads/missing"));
    }

    private static async Task<ParseResult<ReflogEntry>> ReadAsync(
        TestRepository repository,
        string reference,
        int limit = 100)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        return await new ReflogReader(runner).ReadAsync(location, reference, limit, Token);
    }
}
