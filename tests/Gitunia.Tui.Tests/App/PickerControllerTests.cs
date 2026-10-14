using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Execution;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class PickerControllerTests
{
    private static PickerHarness Create(
        string home,
        TimeSpan? listingTimeout = null,
        Func<string, IReadOnlyList<string>>? listDirectories = null,
        TimeSpan? scanningDelay = null)
    {
        var host = new WorkspaceHost(Workspace.Empty);
        var pump = new UiPump(TestContext.Current.CancellationToken);
        // Never show scanning by default: only the tests about it opt in, with a zero delay.
        var picker = new PickerController(TestGit.Services.Discovery, host, home, pump.Post, listingTimeout, listDirectories, scanningDelay ?? Timeout.InfiniteTimeSpan);
        return new PickerHarness(picker, host, pump);
    }

    [Fact]
    public async Task GivenTypedRepositoryPath_WhenAccepting_ThenItOpensInATabAndThePickerCloses()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var (picker, host, pump) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(repository.WorkingTreePath));

        picker.Accept();
        await pump.RunUntilAsync(() => !picker.IsOpen);

        var tab = Assert.Single(host.Workspace.Tabs);
        Assert.Equal(Path.GetFullPath(repository.WorkingTreePath), tab.Location.RootPath);
        Assert.Contains(tab.Location.RootPath, host.Workspace.Recents);
    }

    [Fact]
    public async Task GivenSubdirectoryOfARepository_WhenAccepting_ThenTheRepositoryRootIsOpened()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("sub/a.txt", "1"));
        var (picker, host, pump) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(Path.Combine(repository.WorkingTreePath, "sub")));

        picker.Accept();
        await pump.RunUntilAsync(() => !picker.IsOpen);

        Assert.Equal(Path.GetFullPath(repository.WorkingTreePath), Assert.Single(host.Workspace.Tabs).Location.RootPath);
    }

    [Fact]
    public async Task GivenPathOutsideAnyRepository_WhenAccepting_ThenAMessageIsShownAndThePickerStaysOpen()
    {
        var empty = Path.Combine(Path.GetTempPath(), $"gitunia-picker-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(empty);
        try
        {
            var (picker, host, pump) = Create(Path.GetTempPath());
            picker.Open();
            picker.Apply(new TypeTextCommand(empty));

            picker.Accept();
            await pump.RunUntilAsync(() => picker.State!.Message.Contains("repository", StringComparison.OrdinalIgnoreCase) && !picker.State.Message.StartsWith("opening", StringComparison.Ordinal));

            Assert.True(picker.IsOpen);
            Assert.Empty(host.Workspace.Tabs);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public async Task GivenRepositoryAlreadyOpen_WhenAcceptingItsRow_ThenItsTabIsFocusedWithoutAnotherTab()
    {
        using var first = TestRepository.Create();
        using var second = TestRepository.Create();
        first.Commit("root", new TestFile("a.txt", "1"));
        second.Commit("root", new TestFile("a.txt", "1"));
        var (picker, host, pump) = Create(Path.GetTempPath());
        foreach (var repository in new[] { first, second })
        {
            picker.Open();
            picker.Apply(new TypeTextCommand(repository.WorkingTreePath));
            picker.Accept();
            await pump.RunUntilAsync(() => !picker.IsOpen);
        }

        picker.Open();
        picker.Apply(new TypeTextCommand(Path.GetFileName(first.WorkingTreePath)));
        picker.Accept();

        Assert.False(picker.IsOpen);
        Assert.Equal(2, host.Workspace.Tabs.Length);
        Assert.Equal(host.Workspace.FindTabByRootPath(Path.GetFullPath(first.WorkingTreePath))!.Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public async Task GivenRecentRepository_WhenRemovingItFromRecents_ThenOnlyTheListEntryGoesAndNothingIsDeleted()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var (picker, host, pump) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(repository.WorkingTreePath));
        picker.Accept();
        await pump.RunUntilAsync(() => !picker.IsOpen);
        host.Dispatch(new CloseTabCommand(host.Workspace.ActiveTabId!.Value));
        picker.Open();
        picker.Apply(new TypeTextCommand(Path.GetFileName(repository.WorkingTreePath)));
        Assert.Equal(PickerTags.Recent, picker.State!.HighlightedRow!.Tags & PickerTags.Recent);

        picker.RemoveRecent();

        Assert.Empty(host.Workspace.Recents);
        Assert.True(Directory.Exists(repository.WorkingTreePath));
        Assert.Contains("removed from recents", picker.State!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHighlightedRepository_WhenPinning_ThenItIsPinnedAndUnpinnedAgain()
    {
        var home = Path.Combine(Path.GetTempPath(), "picker-controller-home");
        var path = Path.Combine(home, "code", "alpha");
        var (picker, host, _) = Create(home);
        host.Restore(new WorkspaceUpdate(WorkspaceReducer.Reduce(Workspace.Empty, new RemoveRecentCommand(path)).Workspace with { Recents = [path] }, []));
        picker.Open();

        picker.TogglePin();
        Assert.True(host.Workspace.IsPinned(path));
        Assert.True(picker.State!.HighlightedRow!.IsPinned);

        picker.TogglePin();
        Assert.False(host.Workspace.IsPinned(path));
    }

    [Fact]
    public async Task GivenTypedPath_WhenListing_ThenScanningShowsUntilTheCandidatesArrive()
    {
        using var release = new ManualResetEventSlim();
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: directory =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return [Path.Combine(directory, "alpha")];
        });
        picker.Open();

        picker.Apply(new TypeTextCommand("~/"));

        Assert.True(picker.State!.IsListing);
        Assert.Empty(picker.State.Rows);
        release.Set();
        await pump.RunUntilAsync(() => !picker.State!.IsListing);
        Assert.Equal(["alpha"], picker.State!.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task GivenSlowListing_WhenTypingAgain_ThenTheOlderListingIsDiscarded()
    {
        using var release = new ManualResetEventSlim();
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: directory =>
        {
            if (directory.EndsWith("slow", StringComparison.Ordinal))
            {
                release.Wait(TestContext.Current.CancellationToken);
                return [Path.Combine(directory, "stale")];
            }

            return [Path.Combine(directory, "fresh")];
        });
        picker.Open();
        picker.Apply(new TypeTextCommand("~/slow/"));
        picker.Apply(new TypeTextCommand("x"));
        picker.Apply(new BackspaceCommand());
        picker.Apply(new BackspaceCommand());
        picker.Apply(new BackspaceCommand());
        picker.Apply(new BackspaceCommand());
        picker.Apply(new BackspaceCommand());
        picker.Apply(new BackspaceCommand());
        picker.Apply(new TypeTextCommand("fast/"));

        await pump.RunUntilAsync(() => !picker.State!.IsListing);
        release.Set();

        // The cancelled listing posts nothing back (the unobserved pool task cannot be awaited, so this covers the
        // outcome rather than the guard in isolation): the fresh rows must be intact.
        Assert.Equal("~/fast/", picker.State!.Query);
        Assert.Empty(picker.State.Message);
        Assert.Equal(["fresh"], picker.State.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task GivenSlowListing_WhenItTakesLongerThanTheDelay_ThenScanningIsShownAndOldRowsStayWhileTyping()
    {
        using var release = new ManualResetEventSlim();
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: directory =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return [Path.Combine(directory, "alpha")];
        }, scanningDelay: TimeSpan.Zero);
        picker.Open();
        picker.Apply(new TypeTextCommand("~/"));
        Assert.False(picker.State!.IsScanningShown);

        await pump.RunUntilAsync(() => picker.State!.IsScanningShown);
        release.Set();
        await pump.RunUntilAsync(() => !picker.State!.IsListing);
        Assert.False(picker.State!.IsScanningShown);
        Assert.Equal(["alpha"], picker.State.Rows.Select(row => row.Name));

        release.Reset();
        picker.Apply(new TypeTextCommand("a"));

        Assert.True(picker.State!.IsListing);
        Assert.Equal(["alpha"], picker.State.Rows.Select(row => row.Name));
        release.Set();
        await pump.RunUntilAsync(() => !picker.State!.IsListing);
    }

    [Fact]
    public async Task GivenFastListing_WhenItCompletes_ThenScanningWasNeverShown()
    {
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: directory => [Path.Combine(directory, "alpha")]);
        picker.Open();

        picker.Apply(new TypeTextCommand("~/"));
        await pump.RunUntilAsync(() => !picker.State!.IsListing);

        Assert.False(picker.State!.IsScanningShown);
    }

    [Fact]
    public async Task GivenListingThatNeverFinishes_WhenTheTimeoutPasses_ThenAMessageReplacesScanning()
    {
        using var release = new ManualResetEventSlim();
        var (picker, _, pump) = Create(Path.GetTempPath(), TimeSpan.FromMilliseconds(100), directory =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return [Path.Combine(directory, "late")];
        }, TimeSpan.Zero);
        picker.Open();

        picker.Apply(new TypeTextCommand("~/"));
        await pump.RunUntilAsync(() => picker.State!.IsScanningShown);
        await pump.RunUntilAsync(() => !picker.State!.IsListing);
        release.Set();

        Assert.Contains("timed out", picker.State!.Message, StringComparison.Ordinal);
        Assert.Empty(picker.State.Rows);
    }

    [Fact]
    public async Task GivenListingThatThrows_WhenTyping_ThenTheNoticeReplacesScanning()
    {
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: _ => throw new NotSupportedException("bad path"));
        picker.Open();

        picker.Apply(new TypeTextCommand("~/"));
        await pump.RunUntilAsync(() => !picker.State!.IsListing);

        Assert.Equal("bad path", picker.State!.Message);
    }

    [Fact]
    public async Task GivenTabDuringListing_WhenTheListingCompletes_ThenThePathIsCompleted()
    {
        using var release = new ManualResetEventSlim();
        var (picker, _, pump) = Create(Path.GetTempPath(), listDirectories: directory =>
        {
            release.Wait(TestContext.Current.CancellationToken);
            return [Path.Combine(directory, "alpha"), Path.Combine(directory, "beta")];
        });
        picker.Open();
        picker.Apply(new TypeTextCommand("~/al"));

        picker.Apply(new CompletePathCommand());
        release.Set();
        await pump.RunUntilAsync(() => picker.State!.Query == "~/alpha/");

        Assert.Equal("~/alpha/", picker.State!.Query);
    }
}
