using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.App.Tests.Loading.Diff;

public class WorkingDiffStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenAFile_WhenShowingIt_ThenTheRowsLoadOffTheUiThread()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["one", "two"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        Assert.True(store.Show(Unstaged("a.txt"), 0));
        Assert.Equal(DiffStatus.Loading, store.Status);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "two" });
        Assert.Equal(1, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenAFolderRequest_WhenShown_ThenTheDiffsOfAllItsFilesAreCombined()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "src/a.cs", FakeDiffReader.FileAdding("src/a.cs", ["from a"]));
        reader.SetWorking(WorkingDiffSide.Unstaged, "src/b.cs", FakeDiffReader.FileAdding("src/b.cs", ["from b"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        var entries = new[] { Modified("src/a.cs"), Modified("src/b.cs") };

        var request = new WorkingDiffRequest(
            WorkingDiffSide.Unstaged,
            entries[0],
            new WorkingDiffFolder("src", entries));
        store.Show(request, 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "from a" });
        Assert.Contains(store.Document.Rows, row => row is { Kind: DiffRowKind.Added, Text: "from b" });
    }

    [Fact]
    public async Task GivenTheShownFile_WhenAskingAgainInTheSameGeneration_ThenNothingIsRead()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["one"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        var started = store.Show(Unstaged("a.txt"), 0);

        Assert.False(started);
        Assert.Equal(1, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenAShownFile_WhenTheGenerationChanges_ThenItLoadsAgainAndKeepsItsRowsMeanwhile()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["one"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
        var before = store.Document;
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["one", "changed"]));

        var started = store.Show(Unstaged("a.txt"), 1);

        Assert.True(started);
        Assert.Equal(DiffStatus.Loading, store.Status);
        Assert.Same(before, store.Document);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
        Assert.Contains(store.Document.Rows, row => row.Text == "changed");
        Assert.Equal(2, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenTwoFilesInOneGeneration_WhenGoingBackToTheFirst_ThenItComesFromTheCache()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["a"]));
        reader.SetWorking(WorkingDiffSide.Unstaged, "b.txt", FakeDiffReader.FileAdding("b.txt", ["b"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
        store.Show(Unstaged("b.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && store.Request == Unstaged("b.txt"));

        store.Show(Unstaged("a.txt"), 0);

        Assert.Equal(DiffStatus.Loaded, store.Status);
        Assert.Contains(store.Document.Rows, row => row.Text == "a");
        Assert.Equal(2, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenTheSameFileOnBothSides_WhenShowingEach_ThenEachSideLoadsItsOwnDiff()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["unstaged"]));
        reader.SetWorking(WorkingDiffSide.Staged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["staged"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
        Assert.Contains(store.Document.Rows, row => row.Text == "unstaged");
        store.Show(new WorkingDiffRequest(WorkingDiffSide.Staged, StagedModified("a.txt")), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && store.Document.Rows.Any(row => row.Text == "staged"));

        Assert.Equal(2, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenAFileWithoutChanges_WhenLoaded_ThenARemarkSaysSo()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt");
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal("No changes to show", Assert.Single(store.Document.Rows).Text);
    }

    [Fact]
    public async Task GivenAFailingRead_WhenShowing_ThenTheMessageSaysItAndOnlyANewGenerationRetries()
    {
        var reader = new FakeDiffReader();
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        store.Show(Unstaged("missing.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Failed);

        Assert.StartsWith("Could not load the diff", Assert.Single(store.Document.Rows).Text, StringComparison.Ordinal);
        reader.SetWorking(WorkingDiffSide.Unstaged, "missing.txt", FakeDiffReader.FileAdding("missing.txt", ["now here"]));
        Assert.False(store.Show(Unstaged("missing.txt"), 0));
        Assert.Equal(DiffStatus.Failed, store.Status);
        Assert.True(store.Show(Unstaged("missing.txt"), 1));
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);
    }

    [Fact]
    public async Task GivenALoadInFlight_WhenAnotherFileIsShown_ThenOnlyTheLastOneArrives()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["a"]));
        reader.SetWorking(WorkingDiffSide.Unstaged, "b.txt", FakeDiffReader.FileAdding("b.txt", ["b"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        store.Show(Unstaged("a.txt"), 0);
        store.Show(Unstaged("b.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(Unstaged("b.txt"), store.Request);
        Assert.Contains(store.Document.Rows, row => row.Text == "b");
    }

    [Fact]
    public async Task GivenAShownFile_WhenTheOptionsChange_ThenItLoadsAgainAndTheSameOptionsChangeNothing()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", ["a"]));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.True(store.SetOptions(store.Options.MoreContext()));
        Assert.False(store.SetOptions(store.Options));
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(2, reader.WorkingReadCount);
    }

    [Fact]
    public async Task GivenALongFile_WhenShownWithASmallRowLimit_ThenItIsCutAndLoadAnywayReadsAll()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "a.txt", FakeDiffReader.FileAdding("a.txt", 50));
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader, rowLimit: 10);
        store.Show(Unstaged("a.txt"), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.True(store.IsCut);
        Assert.True(store.LoadAnyway());
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && !store.IsCut);

        Assert.Equal(52, store.Document.Rows.Count);
    }

    [Fact]
    public void GivenNothingToShow_WhenShowingARemark_ThenItIsTheOnlyRowAndAskingAgainDoesNotRepeatTheEvent()
    {
        var reader = new FakeDiffReader();
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);
        var changes = 0;
        store.Changed += () => changes++;

        store.ShowNothing("No file selected");
        store.ShowNothing("No file selected");

        Assert.Equal("No file selected", Assert.Single(store.Document.Rows).Text);
        Assert.Equal(1, changes);
        Assert.Null(store.Request);
    }

    [Fact]
    public async Task GivenAConflictedFileWithoutAPatch_WhenLoaded_ThenARemarkNamesTheConflict()
    {
        var reader = new FakeDiffReader();
        reader.SetWorking(WorkingDiffSide.Unstaged, "f.txt");
        var pump = new UiPump(Token);
        using var store = CreateStore(pump, reader);

        store.Show(new WorkingDiffRequest(WorkingDiffSide.Unstaged, Conflict("f.txt", ConflictKind.DeletedByThem)), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Contains("deleted by them", store.Document.Rows.Single().Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAFolderOfManyFiles_WhenShown_ThenOnlyTheFirstFilesAreReadAndTheRestIsCounted()
    {
        var reader = new FakeDiffReader();
        var entries = Enumerable.Range(0, 5000).Select(index => Modified($"src/f{index}.cs")).ToArray();
        foreach (var entry in entries)
        {
            reader.SetWorking(WorkingDiffSide.Unstaged, entry.Path, FakeDiffReader.FileAdding(entry.Path, ["x"]));
        }

        var pump = new UiPump(Token);
        using var store = new WorkingDiffStore(
            reader,
            FakeCommitDetailsReader.Location,
            pump.Post,
            folderFileLimit: 10);
        var folder = new WorkingDiffFolder("src", entries);

        store.Show(new WorkingDiffRequest(WorkingDiffSide.Unstaged, entries[0], folder), 0);
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded);

        Assert.Equal(10, reader.WorkingReadCount);
        Assert.True(store.IsCut);
        Assert.Contains(
            store.Document.Rows,
            row => row.Text.Contains("4990 more files left out", StringComparison.Ordinal));

        Assert.True(store.LoadAnyway());
        await pump.RunUntilAsync(() => store.Status == DiffStatus.Loaded && !store.IsCut);
        Assert.Equal(5010, reader.WorkingReadCount);
    }

    private static WorkingDiffRequest Unstaged(string path) => new(WorkingDiffSide.Unstaged, Modified(path));

    private static WorkingDiffStore CreateStore(UiPump pump, FakeDiffReader reader, int rowLimit = DiffStore.DefaultRowLimit) =>
        new(reader, FakeCommitDetailsReader.Location, pump.Post, rowLimit);
}
