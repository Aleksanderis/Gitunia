using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.App.Tests;

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
        var host = new Dispatcher(AppState.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = activeId }, []));
        using var poller = new BackgroundStatusPoller(reader, host, InlineUi.Instance);

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
        var host = new Dispatcher(AppState.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = workspace.Tabs[0].Id }, []));
        using var poller = new BackgroundStatusPoller(reader, host, InlineUi.Instance);
        await poller.PollOnceAsync(CancellationToken.None);

        reader.SetDirty(background, false);
        await poller.PollOnceAsync(CancellationToken.None);

        Assert.False(host.Workspace.FindTab(backgroundId)!.Status!.IsDirty);
    }

    [Fact]
    public async Task GivenBackgroundTabThatKeepsFailing_WhenPolledRepeatedly_ThenItIsSkippedForLongerEachTime()
    {
        var reader = new FailingStatusReader();
        var clock = new ManualClock();
        var active = FakeStatusReader.LocationOf("active");
        var failing = FakeStatusReader.LocationOf("failing");
        var workspace = Open(active, failing);
        var host = new Dispatcher(AppState.Empty);
        host.Restore(new WorkspaceUpdate(workspace with { ActiveTabId = workspace.Tabs[0].Id }, []));
        using var poller = new BackgroundStatusPoller(reader, host, InlineUi.Instance, time: clock);
        var interval = TimeSpan.FromSeconds(30);
        var gaps = new List<TimeSpan>();

        for (var failure = 0; failure < 3; failure++)
        {
            await poller.PollOnceAsync(CancellationToken.None);
            gaps.Add(await GapUntilReadAsync(poller, reader, clock, interval));
        }

        Assert.True(gaps[0] >= interval);
        Assert.True(gaps[1] > gaps[0]);
        Assert.True(gaps[2] > gaps[1]);
    }

    private static async Task<TimeSpan> GapUntilReadAsync(
        BackgroundStatusPoller poller,
        FailingStatusReader reader,
        ManualClock clock,
        TimeSpan step)
    {
        var gap = TimeSpan.Zero;
        var reads = reader.Reads;
        while (reader.Reads == reads)
        {
            clock.Advance(step);
            gap += step;
            await poller.PollOnceAsync(CancellationToken.None);
        }

        return gap;
    }

    private sealed class FailingStatusReader : IStatusReader
    {
        public int Reads { get; private set; }

        public Task<StatusSnapshot> ReadStatusAsync(
            RepositoryLocation location,
            bool includeIgnored,
            UntrackedFilesMode untrackedFiles,
            CancellationToken cancellationToken)
        {
            Reads++;

            return Task.FromException<StatusSnapshot>(new TimeoutException("status timed out"));
        }
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
