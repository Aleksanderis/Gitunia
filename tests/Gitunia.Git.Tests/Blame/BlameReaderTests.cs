using Gitunia.Git.Blame;
using Gitunia.Git.Blame.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Blame;

public class BlameReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTwoCommits_WhenBlamingTheHead_ThenEachLineNamesItsCommitAndTheRootIsBoundary()
    {
        using var repository = TestRepository.Create()
            .Commit("first", new TestFile("a.txt", "one\ntwo\n"))
            .Commit("second", new TestFile("a.txt", "one\nTWO\nthree\n"));

        var (content, owners) = await BlameAsync(repository, "a.txt", repository.Head);

        Assert.Equal(["one", "TWO", "three"], content.Lines);
        Assert.Equal(["first", "second", "second"], owners.Select(commit => commit.Summary));
        Assert.True(owners[0].IsBoundary);
        Assert.False(owners[1].IsBoundary);
        Assert.Equal(repository.Head, owners[1].Id);
    }

    [Fact]
    public async Task GivenAnEditedWorkingTreeFile_WhenBlamingTheWorkingTree_ThenNewLinesAreUncommitted()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "one\ntwo\n"));
        repository.WriteFile("a.txt", "one\nchanged\ntwo\n");

        var (content, owners) = await BlameAsync(repository, "a.txt", null);

        Assert.Equal(["one", "changed", "two"], content.Lines);
        Assert.Equal([false, true, false], owners.Select(commit => commit.IsUncommitted));
    }

    [Fact]
    public async Task GivenARenamedFile_WhenBlaming_ThenOldLinesPointToTheCommitWithTheOldPath()
    {
        using var repository = TestRepository.Create()
            .Commit("add old", new TestFile("old.txt", "alpha\nbeta\ngamma\n"));
        repository.Git("mv", "old.txt", "new.txt");
        repository.Git("commit", "-m", "rename");

        var (_, owners) = await BlameAsync(repository, "new.txt", repository.Head);

        Assert.All(owners, commit => Assert.Equal("add old", commit.Summary));
        Assert.All(owners, commit => Assert.Equal("old.txt", commit.Path));
    }

    [Fact]
    public async Task GivenCrlfAndLatin1Files_WhenReadingContent_ThenLinesAreClean()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "one\r\ntwo\r\n"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "latin.txt"), [0x63, 0x61, 0x66, 0xE9, 0x0A]);

        var crlf = await ContentAsync(repository, "a.txt", repository.Head);
        var latin = await ContentAsync(repository, "latin.txt", null);

        Assert.Equal(["one", "two"], crlf.Lines);
        Assert.Equal(["café"], latin.Lines);
    }

    [Fact]
    public async Task GivenABinaryFile_WhenReadingContent_ThenItIsBinaryAtRevisionAndInTheWorkingTree()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x"));
        File.WriteAllBytes(Path.Combine(repository.WorkingTreePath, "b.bin"), [1, 0, 2, 0]);
        repository.Git("add", "b.bin");
        repository.Git("commit", "-m", "binary");

        Assert.Equal(BlameContentKind.Binary, (await ContentAsync(repository, "b.bin", repository.Head)).Kind);
        Assert.Equal(BlameContentKind.Binary, (await ContentAsync(repository, "b.bin", null)).Kind);
    }

    [Fact]
    public async Task GivenMissingFiles_WhenReadingContent_ThenTheWorkingTreeSaysMissingAndARevisionThrows()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x"));

        Assert.Equal(BlameContentKind.Missing, (await ContentAsync(repository, "nope.txt", null)).Kind);
        await Assert.ThrowsAsync<GitCommandFailedException>(() => ContentAsync(repository, "nope.txt", repository.Head));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenBlaming_ThenTheStreamStops()
    {
        using var repository = TestRepository.Create().Commit("first", new TestFile("a.txt", "x\n"));
        var (runner, location) = await OpenAsync(repository);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var unused in new BlameReader(runner)
                .BlameAsync(location, "a.txt", null, null, cancellation.Token))
            {
            }
        });
    }

    private static async Task<Blamed> BlameAsync(
        TestRepository repository,
        string path,
        string? revision)
    {
        var (runner, location) = await OpenAsync(repository);
        var reader = new BlameReader(runner);
        var content = await reader.ReadContentAsync(location, path, revision, Token);
        var owners = new BlameCommit[content.Lines.Count];
        await foreach (var chunk in reader.BlameAsync(location, path, revision, null, Token))
        {
            for (var line = chunk.FirstLine; line < chunk.FirstLine + chunk.LineCount; line++)
            {
                owners[line] = chunk.Commit;
            }
        }

        return new Blamed(content, owners);
    }

    private static async Task<BlameContent> ContentAsync(TestRepository repository, string path, string? revision)
    {
        var (runner, location) = await OpenAsync(repository);

        return await new BlameReader(runner).ReadContentAsync(location, path, revision, Token);
    }

    private static async Task<Opened> OpenAsync(
        TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        var location = await new RepositoryDiscovery(runner).DiscoverAsync(repository.WorkingTreePath, Token);

        return new Opened(runner, location);
    }

    private sealed record Blamed(BlameContent Content, BlameCommit[] Owners);

    private sealed record Opened(GitRunner Runner, RepositoryLocation Location);
}
