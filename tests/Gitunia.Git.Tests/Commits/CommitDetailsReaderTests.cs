using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitDetailsReaderTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly CommitDetailsReader CommitDetails = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<CommitDetails> ReadAsync(TestRepository testRepository, string revision)
    {
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Git("rev-parse", revision).Trim(), out var id));
        var details = await CommitDetails.ReadAsync(location, id, cancellationToken: Token);
        return Assert.IsType<CommitDetails>(details);
    }

    [Fact]
    public async Task GivenRootCommit_WhenReading_ThenFilesAreAddedAndParentsEmpty()
    {
        using var testRepository = TestRepository.Create().Commit("root\n\nlong body", new TestFile("a.txt", "a\n"), new TestFile("dir/b.txt", "b\n"));

        var details = await ReadAsync(testRepository, "HEAD");

        Assert.Empty(details.Parents);
        Assert.Equal("root\n\nlong body", details.Message);
        Assert.Equal("Test", details.AuthorName);
        Assert.Equal(
            [$"a.txt {FileChangeKind.Added}", $"dir/b.txt {FileChangeKind.Added}"],
            details.Files.Select(file => $"{file.Path} {file.Kind}"));
    }

    [Fact]
    public async Task GivenModifyDeleteAndRename_WhenReading_ThenEachHasItsKind()
    {
        using var testRepository = TestRepository.Create()
            .Commit("one", new TestFile("keep.txt", "k\n"), new TestFile("gone.txt", "g\n"), new TestFile("old name.txt", "line1\nline2\nline3\nline4\n"));
        testRepository.Git("mv", "old name.txt", "new ünï.txt");
        testRepository.Git("rm", "-q", "gone.txt");
        testRepository.Commit("two", new TestFile("keep.txt", "k2\n"));

        var details = await ReadAsync(testRepository, "HEAD");

        Assert.Contains(new CommitFileChange("keep.txt", null, FileChangeKind.Modified, null), details.Files);
        Assert.Contains(new CommitFileChange("gone.txt", null, FileChangeKind.Deleted, null), details.Files);
        Assert.Contains(new CommitFileChange("new ünï.txt", "old name.txt", FileChangeKind.Renamed, 100), details.Files);
        Assert.Equal(3, details.Files.Count);
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReading_ThenTwoParentsAndFilesAgainstFirstParent()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();

        var details = await ReadAsync(testRepository, "HEAD");

        Assert.Equal(2, details.Parents.Count);
        var expected = testRepository.Git("diff-tree", "-r", "--name-only", "--no-commit-id", details.Parents[0].ToString(), "HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Order(), details.Files.Select(file => file.Path).Order());
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReadingFilesAgainstTheSecondParent_ThenTheyDifferFromTheFirstParentList()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var details = await ReadAsync(testRepository, "HEAD");

        var secondParent = details.Parents[1];
        var files = await CommitDetails.ReadFilesAsync(location, details.Id, secondParent, cancellationToken: Token);

        var expected = testRepository
            .Git("diff-tree", "-r", "--name-only", "--no-commit-id", secondParent.ToString(), "HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected.Order(), files.Select(file => file.Path).Order());
        Assert.NotEqual(details.Files.Select(file => file.Path).Order(), files.Select(file => file.Path).Order());
        Assert.Equal(0, details.ParentIndex);
    }

    [Fact]
    public async Task GivenCommitWithEmptyChange_WhenReading_ThenNoFiles()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "a\n")).Commit("empty");

        var details = await ReadAsync(testRepository, "HEAD");

        Assert.Empty(details.Files);
        Assert.Equal("empty", details.Subject);
    }

    [Fact]
    public async Task GivenCancelledToken_WhenReading_ThenThrowsCancellation()
    {
        using var testRepository = TestRepository.Create().Commit("root", new TestFile("a.txt", "a\n"));
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Head, out var id));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CommitDetails.ReadAsync(location, id, cancellationToken: source.Token));
    }

    [Fact]
    public async Task GivenACommit_WhenReading_ThenOneProcessRuns()
    {
        using var testRepository = TestRepository.Create()
            .Commit("root", new TestFile("a.txt", "a\n"))
            .Commit("two\n\nbody", new TestFile("b.txt", "b\n"));
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        Assert.True(ObjectId.TryParse(testRepository.Head, out var id));
        var before = Runner.StartedCount;

        var details = await CommitDetails.ReadAsync(location, id, cancellationToken: Token);

        Assert.Equal(1, Runner.StartedCount - before);
        Assert.Equal("two\n\nbody", details!.Message);
        Assert.Equal(["b.txt"], details.Files.Select(file => file.Path));
    }
}
