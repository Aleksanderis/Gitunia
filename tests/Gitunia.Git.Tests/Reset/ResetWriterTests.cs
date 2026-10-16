using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Reset;

public class ResetWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenSoft_WhenResetting_ThenOnlyTheBranchMovesAndTheChangeStaysStaged()
    {
        using var repository = WithTwoCommits();
        var first = FirstCommit(repository);
        var location = await LocateAsync(repository);

        await WriterFor(repository).ResetAsync(location, ResetMode.Soft, first, Token);

        Assert.Equal(first, repository.Head);
        Assert.Equal("M  a.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Equal("2", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenMixed_WhenResetting_ThenTheIndexIsClearedAndTheFilesStay()
    {
        using var repository = WithTwoCommits();
        var first = FirstCommit(repository);
        var location = await LocateAsync(repository);

        await WriterFor(repository).ResetAsync(location, ResetMode.Mixed, first, Token);

        Assert.Equal(first, repository.Head);
        Assert.Equal(" M a.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Equal("2", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenHardOnADirtyTree_WhenResetting_ThenTrackedChangesAreGoneAndUntrackedFilesStay()
    {
        using var repository = WithTwoCommits();
        var first = FirstCommit(repository);
        repository.WriteFile("a.txt", "dirty");
        repository.WriteFile("untracked.txt", "keep");
        var location = await LocateAsync(repository);

        await WriterFor(repository).ResetAsync(location, ResetMode.Hard, first, Token);

        Assert.Equal(first, repository.Head);
        Assert.Equal("?? untracked.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Equal("1", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenAReset_WhenItReturns_ThenItIsTheOldHeadThatOrigHeadAndTheReflogReach()
    {
        using var repository = WithTwoCommits();
        var old = repository.Head;
        var location = await LocateAsync(repository);

        var returned = await WriterFor(repository)
            .ResetAsync(location, ResetMode.Hard, FirstCommit(repository), Token);

        Assert.Equal(old, returned);
        Assert.Equal(old, repository.Git("rev-parse", "ORIG_HEAD").Trim());
        Assert.Contains(old[..7], repository.Git("reflog", "-n", "3", "--format=%h"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenResetting_ThenTheCommandFailsAndNothingMoves()
    {
        using var repository = WithTwoCommits();
        var old = repository.Head;
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => WriterFor(repository)
            .ResetAsync(location, ResetMode.Hard, new string('a', 40), Token));

        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenAnAnnotatedTag_WhenResetting_ThenTheBranchMovesToItsCommit()
    {
        using var repository = WithTwoCommits();
        var first = FirstCommit(repository);
        repository.Git("tag", "-a", "-m", "release", "v1", first);
        var location = await LocateAsync(repository);

        await WriterFor(repository).ResetAsync(location, ResetMode.Soft, "refs/tags/v1", Token);

        Assert.Equal(first, repository.Head);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenResetting_ThenNothingRuns()
    {
        using var repository = WithTwoCommits();
        var old = repository.Head;
        var location = await LocateAsync(repository);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WriterFor(repository)
            .ResetAsync(location, ResetMode.Hard, FirstCommit(repository), cancellation.Token));

        Assert.Equal(old, repository.Head);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

    private static ResetWriter WriterFor(TestRepository repository) => new(repository.CreateIsolatedRunner());

    private static TestRepository WithTwoCommits() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));

    private static string FirstCommit(TestRepository repository) =>
        repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
}
