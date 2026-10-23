using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tags;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Tags;

public class TagWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

    private static TagWriter WriterFor(TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        return new TagWriter(runner, new RemoteRunner(runner));
    }

    [Theory]
    [InlineData("v1.0", true)]
    [InlineData("-leading", false)]
    [InlineData("a..b", false)]
    [InlineData("", false)]
    public async Task GivenName_WhenValidating_ThenGitRulesDecide(string name, bool expected)
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var valid = await WriterFor(repository).IsValidNameAsync(location, name, Token);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public async Task GivenNoMessage_WhenCreating_ThenTheTagIsLightweight()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        await WriterFor(repository).CreateAsync(location, "v1", "HEAD", null, Token);

        Assert.Equal("commit", repository.Git("cat-file", "-t", "refs/tags/v1").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "v1^{commit}").Trim());
    }

    [Fact]
    public async Task GivenAMessage_WhenCreating_ThenTheTagIsAnnotatedWithIt()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        await WriterFor(repository).CreateAsync(location, "v2", "HEAD", "Release two", Token);

        Assert.Equal("tag", repository.Git("cat-file", "-t", "refs/tags/v2").Trim());
        Assert.Contains("Release two", repository.Git("cat-file", "-p", "refs/tags/v2"), StringComparison.Ordinal);
        Assert.Equal(repository.Head, repository.Git("rev-parse", "v2^{commit}").Trim());
    }

    [Fact]
    public async Task GivenExistingTag_WhenCreating_ThenGitRefusesAndTheTagIsUnchanged()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        var before = repository.Git("rev-parse", "v1").Trim();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).CreateAsync(location, "v1", "HEAD~1", null, Token));

        Assert.Equal(before, repository.Git("rev-parse", "v1").Trim());
    }

    [Fact]
    public async Task GivenLocalTag_WhenDeleting_ThenItIsGone()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        var location = await LocateAsync(repository);

        await WriterFor(repository).DeleteAsync(location, "v1", Token);

        Assert.Equal(string.Empty, repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenARemote_WhenListingRemotes_ThenItsNameIsReturned()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        var remotes = await WriterFor(repository).ListRemotesAsync(location, Token);

        Assert.Equal(["origin"], remotes);
    }

    [Fact]
    public async Task GivenNoRemote_WhenListingRemotes_ThenTheListIsEmpty()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var remotes = await WriterFor(repository).ListRemotesAsync(location, Token);

        Assert.Empty(remotes);
    }

    [Fact]
    public async Task GivenTwoTags_WhenPushingOne_ThenOnlyThatTagReachesTheRemote()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1").Tag("v2");
        var location = await LocateAsync(repository);

        await WriterFor(repository).PushAsync(location, "origin", "v1", Token);

        Assert.Equal(["v1"], RemoteTags(repository));
    }

    [Fact]
    public async Task GivenAnAnnotatedTag_WhenPushingIt_ThenTheRemoteHasTheTagObject()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);
        var writer = WriterFor(repository);
        await writer.CreateAsync(location, "v3", "HEAD", "Three", Token);

        await writer.PushAsync(location, "origin", "v3", Token);

        Assert.Equal(
            repository.Git("rev-parse", "refs/tags/v3").Trim(),
            repository.GitIn(repository.OriginPath, ["rev-parse", "refs/tags/v3"]).Trim());
    }

    [Fact]
    public async Task GivenAPushedTag_WhenDeletingOnTheRemote_ThenTheLocalTagStays()
    {
        using var repository = TestRepositories.WithOrigin().Tag("v1");
        repository.Git("push", "-q", "origin", "refs/tags/v1");
        var location = await LocateAsync(repository);

        await WriterFor(repository).DeleteOnRemoteAsync(location, "origin", "v1", Token);

        Assert.Empty(RemoteTags(repository));
        Assert.Equal("v1", repository.Git("tag", "--list").Trim());
    }

    [Fact]
    public async Task GivenAnUnknownRemote_WhenPushing_ThenGitsErrorIsThrown()
    {
        using var repository = TestRepositories.Linear().Tag("v1");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).PushAsync(location, "nowhere", "v1", Token));
    }

    private static string[] RemoteTags(TestRepository repository) =>
        repository.GitIn(repository.OriginPath, ["tag", "--list"])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
