using Gitunia.Git.Discard;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Platform.FileSystem;
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
    public async Task GivenANameThatIsNotUtf8AndNoByteExactDelete_WhenDeletingUntracked_ThenItIsRefusedBeforeGitRuns()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var location = await DiscardSupport.LocationOfAsync(repository);
        var escaped = $"bad{(char)0xDCE9}.txt";
        var discarder = new WorkingTreeDiscarder(DiscardSupport.SharedRunner, new UnsupportedByteExactFileSystem());

        Assert.False(discarder.CanDeleteInvalidNames);
        await Assert.ThrowsAsync<NotSupportedException>(() => discarder.DeleteUntrackedAsync(location, [escaped], Token));
    }

    [Fact]
    public async Task GivenUntrackedNamesThatAreNotUtf8_WhenDeleting_ThenOnlyThoseAreDeletedByTheirBytes()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Names that are not UTF-8 exist on Linux only.");
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("keep.txt", "keep\n").WriteFile("dir/ok.txt", "ok\n");
        Shell(repository, "touch \"bad$(printf '\\351').txt\" \"other$(printf '\\351').txt\"; mkdir -p \"d$(printf '\\351')\"/sub; touch \"d$(printf '\\351')\"/sub/f.txt \"dir/x$(printf '\\351').txt\"");
        var location = await DiscardSupport.LocationOfAsync(repository);
        var discarder = new WorkingTreeDiscarder(DiscardSupport.SharedRunner, new LinuxByteExactFileSystem());
        var bad = $"bad{(char)0xDCE9}.txt";
        var directory = $"d{(char)0xDCE9}/";

        await discarder.DeleteUntrackedAsync(location, [bad, directory, "keep.txt"], Token);

        var left = repository.Git("ls-files", "-o", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries).Order().ToArray();
        Assert.Equal(["dir/ok.txt", "dir/x\ufffd.txt", "other\ufffd.txt"], left);
        Assert.DoesNotContain(Directory.EnumerateFileSystemEntries(repository.WorkingTreePath, "d*"), entry => Path.GetFileName(entry) != "dir");
        Assert.True(discarder.CanDeleteInvalidNames);
    }

    private static void Shell(TestRepository repository, string script)
    {
        var start = new System.Diagnostics.ProcessStartInfo("sh") { WorkingDirectory = repository.WorkingTreePath };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(script);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
