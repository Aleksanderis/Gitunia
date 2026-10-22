using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Stash;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StashReader Stashes = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenRepositoryWithoutStashes_WhenReading_ThenListIsEmpty()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var result = await Stashes.ReadStashesAsync(location, Token);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task GivenTwoStashes_WhenReading_ThenNewestIsFirstWithItsMessage()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.WriteFile("a.txt", "first");
        testRepository.Git("add", "-A");
        testRepository.Git("stash", "push", "-q", "-m", "first stash");
        testRepository.WriteFile("b.txt", "second");
        testRepository.Git("add", "-A");
        testRepository.Git("stash", "push", "-q", "-m", "second stash");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var result = await Stashes.ReadStashesAsync(location, Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(["stash@{0}", "stash@{1}"], result.Items.Select(entry => entry.Name));
        Assert.Contains("second stash", result.Items[0].Message, StringComparison.Ordinal);
        Assert.Contains("first stash", result.Items[1].Message, StringComparison.Ordinal);
        Assert.Equal(testRepository.Git("rev-parse", "stash@{0}").Trim(), result.Items[0].CommitHash);
    }
}
