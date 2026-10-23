using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App;

[Trait("Os", "Independent")]
public class BackgroundStatusPollerTests
{
    [Fact]
    public async Task GivenDirtyBackgroundTab_WhenPolled_ThenOnlyThatTabGetsMarkersAndNothingElseLoads()
    {
        var reader = new FakeStatusReader();
        var active = FakeStatusReader.LocationOf("active");
        var background = FakeStatusReader.LocationOf("background");
        reader.SetDirty(active, true);
        reader.SetDirty(background, true);
        var workspace = Open(active, background);
        var activeId = workspace.Tabs[0].Id;
        var backgroundId = workspace.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = activeId }, []));
        using var poller = new BackgroundStatusPoller(reader, host, InlineUi.Post);

        await poller.PollOnceAsync(CancellationToken.None);

        Assert.True(host.Workspace.FindTab(backgroundId)!.Status!.IsDirty);
        Assert.Null(host.Workspace.FindTab(activeId)!.Status);
        Assert.Null(host.Workspace.FindTab(backgroundId)!.Head);
        Assert.Empty(host.Workspace.FindTab(backgroundId)!.Refs);
    }

    [Fact]
    public async Task GivenBackgroundTabBecomesClean_WhenPolledAgain_ThenMarkerClears()
    {
        var reader = new FakeStatusReader();
        var active = FakeStatusReader.LocationOf("active");
        var background = FakeStatusReader.LocationOf("background");
        reader.SetDirty(background, true);
        var workspace = Open(active, background);
        var backgroundId = workspace.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = workspace.Tabs[0].Id }, []));
        using var poller = new BackgroundStatusPoller(reader, host, InlineUi.Post);
        await poller.PollOnceAsync(CancellationToken.None);

        reader.SetDirty(background, false);
        await poller.PollOnceAsync(CancellationToken.None);

        Assert.False(host.Workspace.FindTab(backgroundId)!.Status!.IsDirty);
    }

    private static Workspace Open(params RepositoryLocation[] locations)
    {
        var workspace = Workspace.Empty;
        foreach (var location in locations)
        {
            workspace = WorkspaceReducer.Reduce(workspace, new OpenRepositoryCommand(location)).Workspace;
        }

        return workspace;
    }
}
