using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Stash;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenRepositoryWithoutStashes_WhenReading_ThenListIsEmpty()
    {
        using var repository = TestRepositories.Linear(1);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var result = await StashReader.ReadStashesAsync(repo, Token);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task GivenTwoStashes_WhenReading_ThenNewestIsFirstWithItsMessage()
    {
        using var repository = TestRepositories.Linear(1);
        repository.WriteFile("a.txt", "first");
        repository.Git("add", "-A");
        repository.Git("stash", "push", "-q", "-m", "first stash");
        repository.WriteFile("b.txt", "second");
        repository.Git("add", "-A");
        repository.Git("stash", "push", "-q", "-m", "second stash");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var result = await StashReader.ReadStashesAsync(repo, Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(["stash@{0}", "stash@{1}"], result.Items.Select(entry => entry.Name));
        Assert.Contains("second stash", result.Items[0].Message, StringComparison.Ordinal);
        Assert.Contains("first stash", result.Items[1].Message, StringComparison.Ordinal);
        Assert.Equal(repository.Git("rev-parse", "stash@{0}").Trim(), result.Items[0].CommitHash);
    }
}
