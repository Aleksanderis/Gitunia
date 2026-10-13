using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class GraphRefCheckTests
{
    [Fact]
    public async Task GivenUnchangedRepositoryWithAnnotatedTagAndRemoteHead_WhenComparing_ThenThereIsNoDifference()
    {
        using var repository = RepositoryWithHistory();
        repository.Git("tag", "-a", "v1", "-m", "release");
        repository.Git("update-ref", "refs/remotes/origin/main", "HEAD");
        repository.Git("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
        var rows = await ReadRowsAsync(repository);

        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    [Fact]
    public async Task GivenDetachedHead_WhenComparing_ThenThereIsNoDifferenceUntilHeadMoves()
    {
        using var repository = RepositoryWithHistory();
        repository.Git("checkout", "-q", "--detach", "HEAD~1");
        var rows = await ReadRowsAsync(repository);
        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));

        repository.Git("checkout", "-q", "--detach", "HEAD~1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    [Fact]
    public async Task GivenEmptyRepository_WhenComparing_ThenThereIsNoDifference()
    {
        using var repository = TestRepository.Create();
        var rows = await ReadRowsAsync(repository);

        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    [Fact]
    public async Task GivenMovedBranch_WhenComparing_ThenItDiffers()
    {
        using var repository = RepositoryWithHistory();
        repository.Branch("topic");
        var rows = await ReadRowsAsync(repository);
        repository.Git("branch", "-f", "topic", "HEAD~1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    [Fact]
    public async Task GivenDeletedTag_WhenComparing_ThenItDiffers()
    {
        using var repository = RepositoryWithHistory();
        repository.Tag("v1");
        var rows = await ReadRowsAsync(repository);
        repository.Git("tag", "-d", "v1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    [Fact]
    public async Task GivenCheckoutOfAnotherBranchOnTheSameCommit_WhenComparing_ThenTheCurrentMarkerDiffers()
    {
        using var repository = RepositoryWithHistory();
        repository.Branch("other");
        var rows = await ReadRowsAsync(repository);
        repository.Git("checkout", "-q", "other");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(repository)));
    }

    private static TestRepository RepositoryWithHistory()
    {
        var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        repository.Commit("second", ("b.txt", "2"));
        repository.Commit("third", ("c.txt", "3"));
        return repository;
    }

    private static async Task<GraphEntry[]> ReadRowsAsync(TestRepository repository)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<RepositorySnapshot> ReadSnapshotAsync(TestRepository repository)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        var result = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.All, CancellationToken.None);
        return result.Snapshot;
    }
}
