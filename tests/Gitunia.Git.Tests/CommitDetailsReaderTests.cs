using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class CommitDetailsReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<CommitDetails> ReadAsync(TestRepository repository, string revision)
    {
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        Assert.True(ObjectId.TryParse(repository.Git("rev-parse", revision).Trim(), out var id));
        var details = await CommitDetailsReader.ReadAsync(repo, id, Token);
        return Assert.IsType<CommitDetails>(details);
    }

    [Fact]
    public async Task GivenRootCommit_WhenReading_ThenFilesAreAddedAndParentsEmpty()
    {
        using var repository = TestRepository.Create().Commit("root\n\nlong body", ("a.txt", "a\n"), ("dir/b.txt", "b\n"));

        var details = await ReadAsync(repository, "HEAD");

        Assert.Empty(details.Parents);
        Assert.Equal("root\n\nlong body", details.Message);
        Assert.Equal("Test", details.AuthorName);
        Assert.Equal(
            [("a.txt", FileChangeKind.Added), ("dir/b.txt", FileChangeKind.Added)],
            details.Files.Select(file => (file.Path, file.Kind)));
    }

    [Fact]
    public async Task GivenModifyDeleteAndRename_WhenReading_ThenEachHasItsKind()
    {
        using var repository = TestRepository.Create()
            .Commit("one", ("keep.txt", "k\n"), ("gone.txt", "g\n"), ("old name.txt", "line1\nline2\nline3\nline4\n"));
        repository.Git("mv", "old name.txt", "new ünï.txt");
        repository.Git("rm", "-q", "gone.txt");
        repository.Commit("two", ("keep.txt", "k2\n"));

        var details = await ReadAsync(repository, "HEAD");

        Assert.Contains(new CommitFileChange("keep.txt", null, FileChangeKind.Modified, null), details.Files);
        Assert.Contains(new CommitFileChange("gone.txt", null, FileChangeKind.Deleted, null), details.Files);
        Assert.Contains(new CommitFileChange("new ünï.txt", "old name.txt", FileChangeKind.Renamed, 100), details.Files);
        Assert.Equal(3, details.Files.Count);
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReading_ThenTwoParentsAndFilesAgainstFirstParent()
    {
        using var repository = TestRepositories.Branchy();

        var details = await ReadAsync(repository, "HEAD");

        Assert.Equal(2, details.Parents.Count);
        var expected = repository.Git("diff-tree", "-r", "--name-only", "--no-commit-id", details.Parents[0].ToString(), "HEAD")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Order(), details.Files.Select(file => file.Path).Order());
    }

    [Fact]
    public async Task GivenCommitWithEmptyChange_WhenReading_ThenNoFiles()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "a\n")).Commit("empty");

        var details = await ReadAsync(repository, "HEAD");

        Assert.Empty(details.Files);
        Assert.Equal("empty", details.Subject);
    }

    [Fact]
    public async Task GivenCancelledToken_WhenReading_ThenThrowsCancellation()
    {
        using var repository = TestRepository.Create().Commit("root", ("a.txt", "a\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        Assert.True(ObjectId.TryParse(repository.Head, out var id));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CommitDetailsReader.ReadAsync(repo, id, source.Token));
    }
}
