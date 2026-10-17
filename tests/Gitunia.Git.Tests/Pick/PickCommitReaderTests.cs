using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Pick;

public class PickCommitReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenSeveralCommits_WhenReading_ThenTheyComeBackInTheGivenOrderWithSubjects()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("b.txt", "2"))
            .Commit("three | with a bar", new TestFile("c.txt", "3"));
        var (reader, location) = await CreateAsync(repository);
        var hashes = new[] { Hash(repository, "HEAD~2"), Hash(repository, "HEAD"), Hash(repository, "HEAD~1") };

        var commits = await reader.ReadAsync(location, hashes, Token);

        Assert.Equal(hashes, commits.Select(commit => commit.Hash));
        Assert.Equal(["one", "three | with a bar", "two"], commits.Select(commit => commit.Subject));
        Assert.All(commits, commit => Assert.False(commit.IsMerge));
    }

    [Fact]
    public async Task GivenAMergeCommit_WhenReading_ThenItsParentsComeWithTheirSubjects()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"))
            .Merge("topic");
        var (reader, location) = await CreateAsync(repository);

        var commits = await reader.ReadAsync(location, [repository.Head], Token);

        var merge = Assert.Single(commits);
        Assert.True(merge.IsMerge);
        Assert.Equal([Hash(repository, "HEAD^1"), Hash(repository, "HEAD^2")], merge.Parents.Select(p => p.Hash));
        Assert.Equal(["main two", "topic two"], merge.Parents.Select(parent => parent.Subject));
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenReading_ThenGitsErrorIsThrown()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var (reader, location) = await CreateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => reader.ReadAsync(location, [new string('a', 40)], Token));
    }

    private static string Hash(TestRepository repository, string revision) =>
        repository.Git("rev-parse", revision).Trim();

    private static async Task<(PickCommitReader Reader, RepositoryLocation Location)> CreateAsync(
        TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);
        return (new PickCommitReader(runner), location);
    }
}
