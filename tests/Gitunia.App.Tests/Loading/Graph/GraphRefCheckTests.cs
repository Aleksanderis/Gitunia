using Gitunia.App.Loading.Graph;
using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.App.Tests.Loading.Graph;

public class GraphRefCheckTests
{
    [Fact]
    public async Task GivenUnchangedRepositoryWithAnnotatedTagAndRemoteHead_WhenComparing_ThenThereIsNoDifference()
    {
        using var testRepository = RepositoryWithHistory();
        testRepository.Git("tag", "-a", "v1", "-m", "release");
        testRepository.Git("update-ref", "refs/remotes/origin/main", "HEAD");
        testRepository.Git("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/main");
        var rows = await ReadRowsAsync(testRepository);

        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    [Fact]
    public async Task GivenDetachedHead_WhenComparing_ThenThereIsNoDifferenceUntilHeadMoves()
    {
        using var testRepository = RepositoryWithHistory();
        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");
        var rows = await ReadRowsAsync(testRepository);
        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));

        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    [Fact]
    public async Task GivenEmptyRepository_WhenComparing_ThenThereIsNoDifference()
    {
        using var testRepository = TestRepository.Create();
        var rows = await ReadRowsAsync(testRepository);

        Assert.False(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    [Fact]
    public async Task GivenMovedBranch_WhenComparing_ThenItDiffers()
    {
        using var testRepository = RepositoryWithHistory();
        testRepository.Branch("topic");
        var rows = await ReadRowsAsync(testRepository);
        testRepository.Git("branch", "-f", "topic", "HEAD~1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    [Fact]
    public async Task GivenDeletedTag_WhenComparing_ThenItDiffers()
    {
        using var testRepository = RepositoryWithHistory();
        testRepository.Tag("v1");
        var rows = await ReadRowsAsync(testRepository);
        testRepository.Git("tag", "-d", "v1");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    [Fact]
    public async Task GivenCheckoutOfAnotherBranchOnTheSameCommit_WhenComparing_ThenTheCurrentMarkerDiffers()
    {
        using var testRepository = RepositoryWithHistory();
        testRepository.Branch("other");
        var rows = await ReadRowsAsync(testRepository);
        testRepository.Git("checkout", "-q", "other");

        Assert.True(GraphRefCheck.Differs(rows, await ReadSnapshotAsync(testRepository)));
    }

    private static TestRepository RepositoryWithHistory()
    {
        var testRepository = TestRepository.Create();
        testRepository.Commit("root", new TestFile("a.txt", "1"));
        testRepository.Commit("second", new TestFile("b.txt", "2"));
        testRepository.Commit("third", new TestFile("c.txt", "3"));
        return testRepository;
    }

    private static async Task<GraphEntry[]> ReadRowsAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var entries = new List<GraphEntry>();
        await foreach (var batch in TestGit.Services.GraphLoader.StreamAsync(location, CommitOrder.ByDate, null, CancellationToken.None))
        {
            entries.AddRange(batch);
        }

        return [.. entries];
    }

    private static async Task<RepositorySnapshot> ReadSnapshotAsync(TestRepository testRepository)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var result = await TestGit.Services.Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, CancellationToken.None);
        return result.Snapshot;
    }
}
