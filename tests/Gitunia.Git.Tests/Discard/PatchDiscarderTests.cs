using Gitunia.Git.Diff.Models;
using Gitunia.Git.Discard;
using Gitunia.Git.Staging.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;
using static Gitunia.Git.Tests.Discard.DiscardSupport;

namespace Gitunia.Git.Tests.Discard;

public class PatchDiscarderTests
{
    private static readonly PatchDiscarder Discarder = new(DiffReaderInstance, new WorkingTreeDiscarder(SharedRunner));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTwoHunks_WhenDiscardingTheFirst_ThenOnlyTheSecondEditRemains()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", Lines(30)));
        repository.WriteFile("f.txt", Lines(30, Edit(2, "TWO"), Edit(28, "TWENTY-EIGHT")));
        var located = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(located, hunk: 0);
        var result = await Discarder.DiscardAsync(located.Location, located.Entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal(Lines(30, Edit(28, "TWENTY-EIGHT")), Read(repository, "f.txt"));
        Assert.Equal(Lines(30), repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenSeveralAddedLines_WhenDiscardingOne_ThenTheOthersStay()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nB2\nc\n");
        var located = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(located, texts: ["B1"]);
        var result = await Discarder.DiscardAsync(located.Location, located.Entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.Applied, result.Outcome);
        Assert.Equal("a\nB2\nc\n", Read(repository, "f.txt"));
    }

    [Fact]
    public async Task GivenARemovedLineAndAnEdit_WhenDiscardingTheRemoval_ThenTheLineComesBackAndTheEditStays()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\nd\n"));
        repository.WriteFile("f.txt", "a\nc\nD\n");
        var located = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(located, texts: ["b"]);
        await Discarder.DiscardAsync(located.Location, located.Entry, chosen, false, Token);

        Assert.Equal("a\nb\nc\nD\n", Read(repository, "f.txt"));
    }

    [Fact]
    public async Task GivenAStagedEditAndAnUnstagedOne_WhenDiscardingTheHunk_ThenTheIndexIsUntouched()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", Lines(30)));
        repository.WriteFile("f.txt", Lines(30, Edit(2, "TWO")));
        repository.Git("add", "f.txt");
        repository.WriteFile("f.txt", Lines(30, Edit(2, "TWO"), Edit(28, "TWENTY-EIGHT")));
        var located = await LocateAsync(repository, "f.txt");

        var chosen = await ChangesAsync(located, hunk: 0);
        await Discarder.DiscardAsync(located.Location, located.Entry, chosen, false, Token);

        Assert.Equal(Lines(30, Edit(2, "TWO")), Read(repository, "f.txt"));
        Assert.Equal(Lines(30, Edit(2, "TWO")), repository.Git("show", ":f.txt"));
    }

    [Fact]
    public async Task GivenAnUntrackedFile_WhenDiscardingItsHunk_ThenTheFileIsDeleted()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("new.txt", "n\n");
        var located = await LocateAsync(repository, "new.txt");

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, [], true, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
    }

    [Fact]
    public async Task GivenLinesPickedInAnUntrackedFile_WhenDiscarding_ThenNothingIsDeleted()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        repository.WriteFile("new.txt", "n\n");
        var located = await LocateAsync(repository, "new.txt");
        var picked = new[] { new ChangedLine(DiffLineKind.Added, 1, "n") };

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, picked, false, Token);

        Assert.Equal(PatchStagingOutcome.NotSplittable, result.Outcome);
        Assert.Equal("n\n", Read(repository, "new.txt"));
    }

    [Fact]
    public async Task GivenADeletedFile_WhenDiscardingItsHunk_ThenTheFileComesBack()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("gone.txt", "x\ny\n"));
        repository.Delete("gone.txt");
        var located = await LocateAsync(repository, "gone.txt");
        var chosen = await ChangesAsync(located, hunk: 0);

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, chosen, true, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal("x\ny\n", Read(repository, "gone.txt"));
    }

    [Fact]
    public async Task GivenAModifiedBinaryFile_WhenDiscardingItsHunk_ThenItIsRestored()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("a.txt", "a\n"));
        var path = Path.Combine(repository.WorkingTreePath, "image.bin");
        File.WriteAllBytes(path, [0, 1, 2, 0, 255]);
        repository.Git("add", "image.bin");
        repository.Git("commit", "-q", "-m", "binary");
        File.WriteAllBytes(path, [9, 9, 0, 9]);
        var located = await LocateAsync(repository, "image.bin");

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, [], true, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal([0, 1, 2, 0, 255], File.ReadAllBytes(path));
    }

    [Fact]
    public async Task GivenAFileEditedAfterTheDiffWasRead_WhenDiscardingItsLines_ThenNothingChanges()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\nc\n"));
        repository.WriteFile("f.txt", "a\nB1\nc\n");
        var located = await LocateAsync(repository, "f.txt");
        var chosen = await ChangesAsync(located, texts: ["B1"]);
        repository.WriteFile("f.txt", "a\nsomething else\nc\n");

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, chosen, false, Token);

        Assert.Equal(PatchStagingOutcome.FileChanged, result.Outcome);
        Assert.Equal("a\nsomething else\nc\n", Read(repository, "f.txt"));
    }

    [Fact]
    public async Task GivenAnEditedFile_WhenDiscardingItWhole_ThenItIsRestoredAsTheIndexHasIt()
    {
        using var repository = TestRepository.Create().Commit("Initial", new TestFile("f.txt", "a\nb\n"));
        repository.WriteFile("f.txt", "a\nB\n");
        var located = await LocateAsync(repository, "f.txt");

        var result = await Discarder.DiscardAsync(located.Location, located.Entry, [], true, Token);

        Assert.Equal(PatchStagingOutcome.WholeFile, result.Outcome);
        Assert.Equal("a\nb\n", Read(repository, "f.txt"));
    }
}
