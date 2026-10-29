using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Refs;

public class AncestryReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly AncestryReader Ancestry = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAnOlderCommit_WhenAskingIfItIsAnAncestor_ThenYes()
    {
        using var repository = TestRepositories.Linear();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var older = repository.Git("rev-parse", "HEAD~1").Trim();

        Assert.True(await Ancestry.IsAncestorAsync(location, older, repository.Head, Token));
        Assert.True(await Ancestry.IsAncestorAsync(location, repository.Head, repository.Head, Token));
    }

    [Fact]
    public async Task GivenANewerCommit_WhenAskingIfItIsAnAncestor_ThenNo()
    {
        using var repository = TestRepositories.Linear();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var older = repository.Git("rev-parse", "HEAD~1").Trim();

        Assert.False(await Ancestry.IsAncestorAsync(location, repository.Head, older, Token));
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenAskingIfItIsAnAncestor_ThenGitFails()
    {
        using var repository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var unknown = new string('a', 40);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Ancestry.IsAncestorAsync(location, unknown, repository.Head, Token));
    }
}
