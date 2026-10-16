using Gitunia.Git.Discard;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Discard;

public class WorkingTreeDiscarderTests
{
    private static readonly WorkingTreeDiscarder Discarder = new(DiscardSupport.SharedRunner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenEditedAndDeletedFiles_WhenRestoring_ThenTheyGoBackAndOthersStay()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("a.txt", "a\n"), new TestFile("b.txt", "b\n"), new TestFile("c.txt", "c\n"));
        repository.WriteFile("a.txt", "a edited\n").WriteFile("c.txt", "c edited\n").Delete("b.txt");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.RestoreAsync(location, ["a.txt", "b.txt"], Token);

        Assert.Equal("a\n", DiscardSupport.Read(repository, "a.txt"));
        Assert.Equal("b\n", DiscardSupport.Read(repository, "b.txt"));
        Assert.Equal("c edited\n", DiscardSupport.Read(repository, "c.txt"));
    }

    [Fact]
    public async Task GivenStagedAndUnstagedEdits_WhenRestoring_ThenTheStagedVersionStaysInFileAndIndex()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("a.txt", "a staged\n");
        repository.Git("add", "a.txt");
        repository.WriteFile("a.txt", "a staged and more\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.RestoreAsync(location, ["a.txt"], Token);

        Assert.Equal("a staged\n", DiscardSupport.Read(repository, "a.txt"));
        Assert.Equal("a staged\n", repository.Git("show", ":a.txt"));
    }

    [Fact]
    public async Task GivenNamesWithGlobCharacters_WhenRestoring_ThenOnlyTheNamedFileIsRestored()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("star*.txt", "1\n"), new TestFile("starry.txt", "2\n"));
        repository.WriteFile("star*.txt", "1 edited\n").WriteFile("starry.txt", "2 edited\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.RestoreAsync(location, ["star*.txt"], Token);

        Assert.Equal("1\n", DiscardSupport.Read(repository, "star*.txt"));
        Assert.Equal("2 edited\n", DiscardSupport.Read(repository, "starry.txt"));
    }

    [Fact]
    public async Task GivenUntrackedFilesAndDirectories_WhenDeleting_ThenOnlyTheNamedOnesAreGone()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("tracked.txt", "t\n"));
        repository.WriteFile("new.txt", "n\n").WriteFile("keep.txt", "k\n").WriteFile("dir/inner.txt", "i\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.DeleteUntrackedAsync(location, ["new.txt", "dir/"], Token);

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
        Assert.False(Directory.Exists(Path.Combine(repository.WorkingTreePath, "dir")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "keep.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "tracked.txt")));
    }

    [Fact]
    public async Task GivenATrackedAndAnIgnoredPath_WhenDeletingUntracked_ThenNeitherIsTouched()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("tracked.txt", "t\n"), new TestFile(".gitignore", "ignored.log\n"));
        repository.WriteFile("ignored.log", "log\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.DeleteUntrackedAsync(location, ["tracked.txt", "ignored.log"], Token);

        Assert.Equal("t\n", DiscardSupport.Read(repository, "tracked.txt"));
        Assert.Equal("log\n", DiscardSupport.Read(repository, "ignored.log"));
    }

    [Fact]
    public async Task GivenAGlobCharacterInAnUntrackedName_WhenDeleting_ThenOnlyThatFileIsGone()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("star*.txt", "1\n").WriteFile("starry.txt", "2\n");
        var location = await DiscardSupport.LocationOfAsync(repository);

        await Discarder.DeleteUntrackedAsync(location, ["star*.txt"], Token);

        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "star*.txt")));
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "starry.txt")));
    }

    [Fact]
    public async Task GivenANameThatIsNotUtf8_WhenDeletingUntracked_ThenItIsRefusedBeforeGitRuns()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var location = await DiscardSupport.LocationOfAsync(repository);
        var escaped = $"bad{(char)0xDCE9}.txt";

        await Assert.ThrowsAsync<NotSupportedException>(() => Discarder.DeleteUntrackedAsync(location, [escaped], Token));
    }
}
