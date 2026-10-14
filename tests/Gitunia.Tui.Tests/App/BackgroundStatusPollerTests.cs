using Gitunia.Core.Workspaces;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class BackgroundStatusPollerTests
{
    [Fact]
    public async Task GivenDirtyBackgroundTab_WhenPolled_ThenOnlyThatTabGetsMarkersAndNothingElseLoads()
    {
        using var active = TestRepository.Create();
        active.Commit("root", new TestFile("a.txt", "1"));
        using var background = TestRepository.Create();
        background.Commit("root", new TestFile("a.txt", "1")).WriteFile("a.txt", "changed");
        var runner = new GitRunner();
        var workspace = await OpenAsync(runner, active.WorkingTreePath, background.WorkingTreePath);
        var activeId = workspace.Tabs[0].Id;
        var backgroundId = workspace.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = activeId }, []));
        using var poller = new BackgroundStatusPoller(runner, host, InlineUi.Post);

        await poller.PollOnceAsync(CancellationToken.None);

        Assert.True(host.Workspace.FindTab(backgroundId)!.Status!.IsDirty);
        Assert.Null(host.Workspace.FindTab(activeId)!.Status);
        Assert.Null(host.Workspace.FindTab(backgroundId)!.Head);
        Assert.Empty(host.Workspace.FindTab(backgroundId)!.Refs);
    }

    [Fact]
    public async Task GivenBackgroundTabBecomesClean_WhenPolledAgain_ThenMarkerClears()
    {
        using var active = TestRepository.Create();
        active.Commit("root", new TestFile("a.txt", "1"));
        using var background = TestRepository.Create();
        background.Commit("root", new TestFile("a.txt", "1")).WriteFile("a.txt", "changed");
        var runner = new GitRunner();
        var workspace = await OpenAsync(runner, active.WorkingTreePath, background.WorkingTreePath);
        var backgroundId = workspace.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = workspace.Tabs[0].Id }, []));
        using var poller = new BackgroundStatusPoller(runner, host, InlineUi.Post);
        await poller.PollOnceAsync(CancellationToken.None);

        background.Git("checkout", "--", "a.txt");
        await poller.PollOnceAsync(CancellationToken.None);

        Assert.False(host.Workspace.FindTab(backgroundId)!.Status!.IsDirty);
    }

    private static async Task<Workspace> OpenAsync(GitRunner runner, params string[] paths)
    {
        var workspace = Workspace.Empty;
        foreach (var path in paths)
        {
            var location = await RepositoryDiscovery.DiscoverAsync(runner, path, CancellationToken.None);
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(location)).Workspace;
        }

        return workspace;
    }
}
