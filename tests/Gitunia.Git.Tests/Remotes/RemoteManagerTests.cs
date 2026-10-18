using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Remotes;

public class RemoteManagerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

    private static RemoteManager ManagerFor(TestRepository repository) => new(repository.CreateIsolatedRunner());

    [Fact]
    public async Task GivenOrigin_WhenListing_ThenTheNameAndFetchUrlAreReturned()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        var remotes = await ManagerFor(repository).ListAsync(location, Token);

        var origin = Assert.Single(remotes);
        Assert.Equal("origin", origin.Name);
        Assert.Equal(repository.OriginPath, origin.Url);
    }

    [Fact]
    public async Task GivenNoRemote_WhenListing_ThenTheListIsEmpty()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        Assert.Empty(await ManagerFor(repository).ListAsync(location, Token));
    }

    [Fact]
    public async Task GivenAUrl_WhenAdding_ThenTheRemoteIsConfiguredAndNothingIsFetched()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        await ManagerFor(repository).AddAsync(location, "backup", repository.OriginPath, Token);

        Assert.Equal(repository.OriginPath, repository.Git("config", "remote.backup.url").Trim());
        Assert.DoesNotContain("backup/", repository.Git("branch", "-r"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnExistingName_WhenAdding_ThenGitRefusesAndTheUrlIsUnchanged()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => ManagerFor(repository).AddAsync(location, "origin", "elsewhere", Token));

        Assert.Equal(repository.OriginPath, repository.Git("config", "remote.origin.url").Trim());
    }

    [Fact]
    public async Task GivenTrackingBranches_WhenRemoving_ThenTheRemoteTheirRefsAndTheUpstreamAreGone()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);
        Assert.Contains("origin/topic", repository.Git("branch", "-r"), StringComparison.Ordinal);

        await ManagerFor(repository).RemoveAsync(location, "origin", Token);

        Assert.Empty(await ManagerFor(repository).ListAsync(location, Token));
        Assert.Equal(string.Empty, repository.Git("branch", "-r").Trim());
        Assert.DoesNotContain("branch.topic.remote", repository.Git("config", "--list"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnknownRemote_WhenRemoving_ThenGitRefuses()
    {
        using var repository = TestRepositories.WithOrigin();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => ManagerFor(repository).RemoveAsync(location, "nope", Token));

        Assert.Single(await ManagerFor(repository).ListAsync(location, Token));
    }

    [Fact]
    public async Task GivenTrackingBranches_WhenRenaming_ThenRefsAndUpstreamMoveToTheNewName()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        await ManagerFor(repository).RenameAsync(location, "origin", "upstream", Token);

        var remotes = await ManagerFor(repository).ListAsync(location, Token);
        Assert.Equal("upstream", Assert.Single(remotes).Name);
        var branches = repository.Git("branch", "-r");
        Assert.Contains("upstream/topic", branches, StringComparison.Ordinal);
        Assert.DoesNotContain("origin/", branches, StringComparison.Ordinal);
        Assert.Equal("upstream", repository.Git("config", "branch.topic.remote").Trim());
    }

    [Fact]
    public async Task GivenTheNameOfAnotherRemote_WhenRenaming_ThenGitRefusesAndBothStay()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "other", "somewhere");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => ManagerFor(repository).RenameAsync(location, "origin", "other", Token));

        Assert.Equal(2, (await ManagerFor(repository).ListAsync(location, Token)).Count);
    }

    [Fact]
    public async Task GivenANewUrl_WhenSettingIt_ThenOnlyThatRemoteChanges()
    {
        using var repository = TestRepositories.WithOrigin();
        repository.Git("remote", "add", "other", "somewhere");
        var location = await LocateAsync(repository);

        await ManagerFor(repository).SetUrlAsync(location, "origin", "https://example.com/x.git", Token);

        var remotes = await ManagerFor(repository).ListAsync(location, Token);
        Assert.Equal("https://example.com/x.git", remotes.Single(remote => remote.Name == "origin").Url);
        Assert.Equal("somewhere", remotes.Single(remote => remote.Name == "other").Url);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenWriting_ThenNothingRuns()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ManagerFor(repository).AddAsync(location, "late", "x", source.Token));

        Assert.Empty(await ManagerFor(repository).ListAsync(location, Token));
    }
}
