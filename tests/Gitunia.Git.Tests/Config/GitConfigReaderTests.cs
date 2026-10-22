using Gitunia.Git.Config;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Config;

public class GitConfigReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNoSetting_WhenReadingRebaseAutoStash_ThenItIsOff()
    {
        using var testRepository = TestRepositories.Linear(1);
        var runner = testRepository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(testRepository.WorkingTreePath, Token);
        var config = new GitConfigReader(runner);

        Assert.False(await config.ReadRebaseAutoStashAsync(location, Token));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    public async Task GivenTheSetting_WhenReadingRebaseAutoStash_ThenItIsReadAsABoolean(string value, bool expected)
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Git("config", "rebase.autoStash", value);
        var runner = testRepository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(testRepository.WorkingTreePath, Token);
        var config = new GitConfigReader(runner);

        Assert.Equal(expected, await config.ReadRebaseAutoStashAsync(location, Token));
    }
}
