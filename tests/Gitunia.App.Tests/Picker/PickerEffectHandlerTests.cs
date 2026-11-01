using Gitunia.App.Picker;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Picker;

// Real git and real directories for the discovery and the scan; a scripted file system for the slow listings.
public class PickerEffectHandlerTests
{
    [Fact]
    public async Task GivenConfiguredScanRoots_WhenOpening_ThenOnlyRepositoriesBelowThemAreDiscovered()
    {
        var configured = Directory.CreateTempSubdirectory("gitunia-roots-");
        var home = Directory.CreateTempSubdirectory("gitunia-home-");
        Directory.CreateDirectory(Path.Combine(configured.FullName, "inside", ".git"));
        Directory.CreateDirectory(Path.Combine(home.FullName, "code", "outside", ".git"));
        try
        {
            using var pipeline = PickerPipeline.Create(home.FullName, scanRoots: [configured.FullName]);

            pipeline.Open();
            await pipeline.Pump.RunUntilAsync(() => pipeline.State.Rows.Length > 0);

            Assert.Equal(["inside"], pipeline.State.Rows.Select(row => row.Name));
        }
        finally
        {
            configured.Delete(recursive: true);
            home.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GivenNoScanRootsConfigured_WhenOpening_ThenTheConventionalFoldersBelowHomeAreScanned()
    {
        var home = Directory.CreateTempSubdirectory("gitunia-home-");
        Directory.CreateDirectory(Path.Combine(home.FullName, "code", "found", ".git"));
        try
        {
            using var pipeline = PickerPipeline.Create(home.FullName);
            pipeline.Dispatch(new PickerScanRootsChanged(null));

            pipeline.Open();
            await pipeline.Pump.RunUntilAsync(() => pipeline.State.Rows.Length > 0);

            Assert.Equal(["found"], pipeline.State.Rows.Select(row => row.Name));
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GivenAnEmptyScanRootList_WhenOpening_ThenNothingIsDiscovered()
    {
        var home = Directory.CreateTempSubdirectory("gitunia-home-");
        Directory.CreateDirectory(Path.Combine(home.FullName, "code", "outside", ".git"));
        try
        {
            using var pipeline = PickerPipeline.Create(home.FullName, scanRoots: []);

            pipeline.Open();
            await pipeline.Pump.RunUntilAsync(() => !pipeline.Host.State.Shell.Picker.IsScanning);

            Assert.Empty(pipeline.State.Rows);
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public async Task GivenMissingTabWithADraft_WhenLocatingIt_ThenTheDraftMovesToTheNewTab(bool moved, int expectedTabs)
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var pipeline = PickerPipeline.Create();
        pipeline.Shell.DraftMoveResult = moved;
        var gone = Path.Combine(Path.GetTempPath(), $"gitunia-gone-{Guid.NewGuid():N}");
        pipeline.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(gone, gone, gone, false, false)));
        var missing = pipeline.Host.Workspace.ActiveTabId!.Value;
        pipeline.Dispatch(new PickerOpenRequested(missing, string.Empty, pipeline.Host.Clock.GetUtcNow()));
        pipeline.Type(repository.WorkingTreePath);

        pipeline.Dispatch(new PickerAcceptPressed());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Shell.DraftMoves.Count > 0);

        var move = Assert.Single(pipeline.Shell.DraftMoves);
        Assert.Equal(missing, move.Missing);
        var target = pipeline.Host.Workspace.FindTab(move.Target)!;
        Assert.Equal(Path.GetFullPath(repository.WorkingTreePath), target.Location.RootPath);
        Assert.Equal(expectedTabs, pipeline.Host.Workspace.Tabs.Length);
        Assert.Equal(moved, pipeline.Host.Workspace.FindTab(missing) is null);
        Assert.Equal(!moved, pipeline.IsOpen);
    }

    [Fact]
    public async Task GivenMissingTabLocatedAtItsOwnPath_WhenOpening_ThenTheTabStaysWithoutMovingItsDraft()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var pipeline = PickerPipeline.Create();
        var root = Path.GetFullPath(repository.WorkingTreePath);
        pipeline.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(root, root, root, false, false)));
        var tab = pipeline.Host.Workspace.ActiveTabId!.Value;
        pipeline.Dispatch(new PickerOpenRequested(tab, string.Empty, pipeline.Host.Clock.GetUtcNow()));
        pipeline.Type(repository.WorkingTreePath);

        pipeline.Dispatch(new PickerAcceptPressed());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsOpen);

        Assert.Equal(tab, Assert.Single(pipeline.Host.Workspace.Tabs).Id);
        Assert.Empty(pipeline.Shell.DraftMoves);
    }

    [Fact]
    public async Task GivenTypedRepositoryPath_WhenAccepting_ThenItOpensInATabAndThePickerCloses()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var pipeline = PickerPipeline.Create();
        pipeline.Open();
        pipeline.Type(repository.WorkingTreePath);

        pipeline.Dispatch(new PickerAcceptPressed());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsOpen);

        var tab = Assert.Single(pipeline.Host.Workspace.Tabs);
        Assert.Equal(Path.GetFullPath(repository.WorkingTreePath), tab.Location.RootPath);
        Assert.Contains(tab.Location.RootPath, pipeline.Host.Workspace.Recents);
    }

    [Fact]
    public async Task GivenSubdirectoryOfARepository_WhenAccepting_ThenTheRepositoryRootIsOpened()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("sub/a.txt", "1"));
        using var pipeline = PickerPipeline.Create();
        pipeline.Open();
        pipeline.Type(Path.Combine(repository.WorkingTreePath, "sub"));

        pipeline.Dispatch(new PickerAcceptPressed());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsOpen);

        var tab = Assert.Single(pipeline.Host.Workspace.Tabs);
        Assert.Equal(Path.GetFullPath(repository.WorkingTreePath), tab.Location.RootPath);
    }

    [Fact]
    public async Task GivenPathOutsideAnyRepository_WhenAccepting_ThenAMessageIsShownAndThePickerStaysOpen()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"gitunia-picker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);
        try
        {
            using var pipeline = PickerPipeline.Create();
            pipeline.Open();
            pipeline.Type(empty);

            pipeline.Dispatch(new PickerAcceptPressed());
            await pipeline.Pump.RunUntilAsync(() => IsFailureShown(pipeline));

            Assert.True(pipeline.IsOpen);
            Assert.Empty(pipeline.Host.Workspace.Tabs);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public async Task GivenDirectoryThatDoesNotExist_WhenAccepting_ThenTheMessageSaysSo()
    {
        using var pipeline = PickerPipeline.Create();
        pipeline.Open();
        pipeline.Type(Path.Combine(Path.GetTempPath(), $"gitunia-none-{Guid.NewGuid():N}"));

        pipeline.Dispatch(new PickerAcceptPressed());
        await pipeline.Pump.RunUntilAsync(() => IsFailureShown(pipeline));

        Assert.Contains("does not exist", pipeline.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTypedPath_WhenListing_ThenScanningShowsUntilTheCandidatesArrive()
    {
        using var release = new ManualResetEventSlim();
        using var pipeline = PipelineWith(directory => Blocked(release, directory, "alpha"));
        pipeline.Open();

        pipeline.Type("~/");

        Assert.True(pipeline.State.IsListing);
        Assert.Empty(pipeline.State.Rows);
        release.Set();
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);
        Assert.Equal(["alpha"], pipeline.State.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task GivenSlowListing_WhenTypingAgain_ThenTheOlderListingIsDiscarded()
    {
        using var release = new ManualResetEventSlim();
        using var pipeline = PipelineWith(directory =>
        {
            if (directory.EndsWith("slow", StringComparison.Ordinal))
            {
                return Blocked(release, directory, "stale");
            }

            return [Path.Combine(directory, "fresh")];
        });
        pipeline.Open();
        pipeline.Type("~/slow/");
        pipeline.Type("x");
        for (var count = 0; count < "slow/x".Length; count++)
        {
            pipeline.Dispatch(new BackspaceCommand());
        }

        pipeline.Type("fast/");
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);
        release.Set();

        // The cancelled listing posts nothing back: the fresh rows must be intact.
        Assert.Equal("~/fast/", pipeline.State.Query);
        Assert.Empty(pipeline.State.Message);
        Assert.Equal(["fresh"], pipeline.State.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task GivenSlowListing_WhenItTakesLongerThanTheDelay_ThenScanningIsShownAndOldRowsStayWhileTyping()
    {
        using var release = new ManualResetEventSlim();
        var timings = new PickerTimings(TimeSpan.FromSeconds(5), TimeSpan.Zero);
        var files = new ScriptedDirectories(directory => Blocked(release, directory, "alpha"));
        using var pipeline = PickerPipeline.Create(picker: new PickerServices(files, timings));
        pipeline.Open();
        pipeline.Type("~/");
        Assert.False(pipeline.State.IsScanningShown);

        await pipeline.Pump.RunUntilAsync(() => pipeline.State.IsScanningShown);
        release.Set();
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);
        Assert.False(pipeline.State.IsScanningShown);
        Assert.Equal(["alpha"], pipeline.State.Rows.Select(row => row.Name));

        release.Reset();
        pipeline.Type("a");

        Assert.True(pipeline.State.IsListing);
        Assert.Equal(["alpha"], pipeline.State.Rows.Select(row => row.Name));
        release.Set();
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);
    }

    [Fact]
    public async Task GivenFastListing_WhenItCompletes_ThenScanningWasNeverShown()
    {
        using var pipeline = PipelineWith(directory => [Path.Combine(directory, "alpha")]);
        pipeline.Open();

        pipeline.Type("~/");
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);

        Assert.False(pipeline.State.IsScanningShown);
    }

    [Fact]
    public async Task GivenListingThatNeverFinishes_WhenTheTimeoutPasses_ThenAMessageReplacesScanning()
    {
        using var release = new ManualResetEventSlim();
        var timings = new PickerTimings(TimeSpan.FromMilliseconds(100), TimeSpan.Zero);
        var files = new ScriptedDirectories(directory => Blocked(release, directory, "late"));
        using var pipeline = PickerPipeline.Create(picker: new PickerServices(files, timings));
        pipeline.Open();

        pipeline.Type("~/");
        await pipeline.Pump.RunUntilAsync(() => pipeline.State.IsScanningShown);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);
        release.Set();

        Assert.Contains("timed out", pipeline.State.Message, StringComparison.Ordinal);
        Assert.Empty(pipeline.State.Rows);
    }

    [Fact]
    public async Task GivenListingThatThrows_WhenTyping_ThenTheNoticeReplacesScanning()
    {
        using var pipeline = PipelineWith(_ => throw new NotSupportedException("bad path"));
        pipeline.Open();

        pipeline.Type("~/");
        await pipeline.Pump.RunUntilAsync(() => !pipeline.State.IsListing);

        Assert.Equal("bad path", pipeline.State.Message);
    }

    [Fact]
    public async Task GivenTabDuringListing_WhenTheListingCompletes_ThenThePathIsCompleted()
    {
        using var release = new ManualResetEventSlim();
        using var pipeline = PipelineWith(directory => Blocked(release, directory, "alpha", "beta"));
        pipeline.Open();
        pipeline.Type("~/al");

        pipeline.Dispatch(new CompletePathCommand());
        release.Set();
        await pipeline.Pump.RunUntilAsync(() => pipeline.State.Query == "~/alpha/");

        Assert.Equal("~/alpha/", pipeline.State.Query);
    }

    [Fact]
    public async Task GivenListing_WhenThePickerCloses_ThenTheListingIsCancelledAndPostsNothing()
    {
        using var release = new ManualResetEventSlim();
        using var pipeline = PipelineWith(directory => Blocked(release, directory, "alpha"));
        pipeline.Open();
        pipeline.Type("~/");

        pipeline.Dispatch(new PickerCloseRequested());
        release.Set();
        pipeline.Open();

        // A new open starts its own state; the cancelled listing must not fill it.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Empty(pipeline.State.Rows);
        Assert.False(pipeline.State.IsListing);
    }

    private static PickerPipeline PipelineWith(Func<string, IReadOnlyList<string>> list) =>
        PickerPipeline.Create(picker: PickerPipeline.NeverScanning(new ScriptedDirectories(list)));

    private static IReadOnlyList<string> Blocked(ManualResetEventSlim release, string directory, params string[] names)
    {
        Assert.True(release.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        return [.. names.Select(name => Path.Combine(directory, name))];
    }

    private static bool IsFailureShown(PickerPipeline pipeline) =>
        pipeline.State.Message.Length > 0 && !pipeline.State.Message.StartsWith("opening", StringComparison.Ordinal);
}
