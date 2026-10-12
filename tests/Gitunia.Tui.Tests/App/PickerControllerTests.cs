using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class PickerControllerTests
{
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting for the picker to finish");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private static (PickerController Picker, WorkspaceHost Host) Create(string home)
    {
        var host = new WorkspaceHost(Workspace.Empty);
        // Background results are posted back inline: the test thread only polls.
        var picker = new PickerController(new GitRunner(), host, home, action => action());
        return (picker, host);
    }

    [Fact]
    public async Task GivenTypedRepositoryPath_WhenAccepting_ThenItOpensInATabAndThePickerCloses()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var (picker, host) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(repository.Path));

        picker.Accept();
        await WaitForAsync(() => !picker.IsOpen);

        var tab = Assert.Single(host.Workspace.Tabs);
        Assert.Equal(Path.GetFullPath(repository.Path), tab.Location.RootPath);
        Assert.Contains(tab.Location.RootPath, host.Workspace.Recents);
    }

    [Fact]
    public async Task GivenSubdirectoryOfARepository_WhenAccepting_ThenTheRepositoryRootIsOpened()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("sub/a.txt", "1"));
        var (picker, host) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(Path.Combine(repository.Path, "sub")));

        picker.Accept();
        await WaitForAsync(() => !picker.IsOpen);

        Assert.Equal(Path.GetFullPath(repository.Path), Assert.Single(host.Workspace.Tabs).Location.RootPath);
    }

    [Fact]
    public async Task GivenPathOutsideAnyRepository_WhenAccepting_ThenAMessageIsShownAndThePickerStaysOpen()
    {
        var empty = Path.Combine(Path.GetTempPath(), "gitunia-picker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            var (picker, host) = Create(Path.GetTempPath());
            picker.Open();
            picker.Apply(new TypeTextCommand(empty));

            picker.Accept();
            await WaitForAsync(() => picker.State!.Message.Contains("repository", StringComparison.OrdinalIgnoreCase) && !picker.State.Message.StartsWith("opening", StringComparison.Ordinal));

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
        first.Commit("root", ("a.txt", "1"));
        second.Commit("root", ("a.txt", "1"));
        var (picker, host) = Create(Path.GetTempPath());
        foreach (var repository in new[] { first, second })
        {
            picker.Open();
            picker.Apply(new TypeTextCommand(repository.Path));
            picker.Accept();
            await WaitForAsync(() => !picker.IsOpen);
        }

        picker.Open();
        picker.Apply(new TypeTextCommand(Path.GetFileName(first.Path)));
        picker.Accept();

        Assert.False(picker.IsOpen);
        Assert.Equal(2, host.Workspace.Tabs.Length);
        Assert.Equal(host.Workspace.FindTabByRootPath(Path.GetFullPath(first.Path))!.Id, host.Workspace.ActiveTabId);
    }

    [Fact]
    public async Task GivenRecentRepository_WhenRemovingItFromRecents_ThenOnlyTheListEntryGoesAndNothingIsDeleted()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var (picker, host) = Create(Path.GetTempPath());
        picker.Open();
        picker.Apply(new TypeTextCommand(repository.Path));
        picker.Accept();
        await WaitForAsync(() => !picker.IsOpen);
        host.Dispatch(new CloseTabCommand(host.Workspace.ActiveTabId!.Value));
        picker.Open();
        picker.Apply(new TypeTextCommand(Path.GetFileName(repository.Path)));
        Assert.Equal(PickerTags.Recent, picker.State!.HighlightedRow!.Tags & PickerTags.Recent);

        picker.RemoveRecent();

        Assert.Empty(host.Workspace.Recents);
        Assert.True(Directory.Exists(repository.Path));
        Assert.Contains("removed from recents", picker.State!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenHighlightedRepository_WhenPinning_ThenItIsPinnedAndUnpinnedAgain()
    {
        var home = Path.Combine(Path.GetTempPath(), "picker-controller-home");
        var path = Path.Combine(home, "code", "alpha");
        var (picker, host) = Create(home);
        host.Restore(new WorkspaceUpdate(WorkspaceReducer.Reduce(Workspace.Empty, new RemoveRecentCommand(path)).Workspace with { Recents = [path] }, []));
        picker.Open();

        picker.TogglePin();
        Assert.True(host.Workspace.IsPinned(path));
        Assert.True(picker.State!.HighlightedRow!.IsPinned);

        picker.TogglePin();
        Assert.False(host.Workspace.IsPinned(path));
    }
}
