using Gitunia.Core.Sidebar;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App.Sidebar;
using Terminal.Gui.Views;
using Xunit;

namespace Gitunia.Tui.Tests.App.Sidebar;

public class SidebarPresenterTests
{
    private static async Task<(SidebarHarness Harness, ListView List, TestRepository Repository)> StartAsync()
    {
        var repository = TestRepository.Create();
        repository.Commit("one", new TestFile("a.txt", "1")).Branch("old");
        var harness = await SidebarHarness.LoadAsync(repository.WorkingTreePath);
        var list = new ListView { Source = harness.Source, Width = 40, Height = 10 };
        _ = new SidebarPresenter(harness.Host, list, harness.Source);
        harness.Send(new SetSidebarCursorCommand(1));
        list.Layout();
        list.Draw();
        return (harness, list, repository);
    }

    [Fact]
    public async Task GivenAnUnrelatedCommand_WhenTheStateChanges_ThenTheSidebarIsNotMarkedForRedraw()
    {
        var (harness, list, repository) = await StartAsync();
        using var _ = repository;
        using var __ = harness;

        harness.Host.Dispatch(new TabCommand(harness.Tab.Id, new FocusPaneCommand(PaneKind.Graph)));

        Assert.False(list.NeedsDraw);
    }

    [Fact]
    public async Task GivenACursorMove_WhenTheStateChanges_ThenTheSidebarIsMarkedForRedraw()
    {
        var (harness, list, repository) = await StartAsync();
        using var _ = repository;
        using var __ = harness;

        harness.Send(new SetSidebarCursorCommand(2));

        Assert.True(list.NeedsDraw);
        Assert.Equal(2, list.SelectedItem);
    }
}
