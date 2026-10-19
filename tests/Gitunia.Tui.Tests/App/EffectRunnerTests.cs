using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App;

// The restored-tab case runs on fakes: it is about which tabs load. The other cases are end-to-end checks of the wiring (real file system
// watchers reacting to real git commands, and a manual refresh through the real refresher), so they keep real temporary repositories.
public class EffectRunnerTests
{
    [Fact]
    public async Task GivenRestoredTabs_WhenStarting_ThenOnlyActiveTabLoadsUntilActivated()
    {
        var fakes = new FakeGitServices();
        fakes.CommitLog.AddOnTop("root");
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var scratch = Workspace.Empty;
        foreach (var name in new[] { "first", "second" })
        {
            scratch = WorkspaceReducer.Reduce(scratch, new OpenRepositoryCommand(FakeStatusReader.LocationOf(name))).Workspace;
        }

        var firstId = scratch.Tabs[0].Id;
        var secondId = scratch.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        using var effects = new EffectRunner(fakes.Services, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel(), watcherOptions: TestGit.QuietWatcherOptions);

        host.Restore(new WorkspaceUpdate(
            scratch with { ActiveTabId = firstId },
            [new LoadRepositoryEffect(firstId, scratch.Tabs[0].CommandPath), new LoadRepositoryEffect(secondId, scratch.Tabs[1].CommandPath)]));

        Assert.False(effects.SessionOf(firstId)!.IsPending);
        Assert.True(effects.SessionOf(secondId)!.IsPending);
        await pump.RunUntilAsync(() => !effects.SessionOf(firstId)!.IsLoading);
        Assert.Equal(0, effects.SessionOf(secondId)!.Graph.Count);

        host.Dispatch(new ActivateTabCommand(secondId));

        Assert.False(effects.SessionOf(secondId)!.IsPending);
        await pump.RunUntilAsync(() => !effects.SessionOf(secondId)!.IsLoading);
        Assert.Equal(1, effects.SessionOf(secondId)!.Graph.Count);
    }

    [Fact]
    public async Task GivenMoreVisitedTabsThanLimit_WhenSwitching_ThenColdestTabIsEvictedAndReloadsOnActivation()
    {
        var fakes = new FakeGitServices();
        fakes.CommitLog.AddOnTop("root");
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var scratch = Workspace.Empty;
        foreach (var name in new[] { "first", "second", "third" })
        {
            var location = FakeStatusReader.LocationOf(name);
            scratch = WorkspaceReducer.Reduce(scratch, new OpenRepositoryCommand(location)).Workspace;
        }

        var ids = scratch.Tabs.Select(tab => tab.Id).ToArray();
        var host = new WorkspaceHost(Workspace.Empty);
        using var effects = new EffectRunner(
            fakes.Services,
            host,
            pump.Post,
            GraphGlyphSet.Unicode,
            new StatusChannel(),
            watcherOptions: TestGit.QuietWatcherOptions,
            warmTabLimit: 2);
        host.Restore(new WorkspaceUpdate(
            scratch with { ActiveTabId = ids[0] },
            [.. scratch.Tabs.Select(tab => new LoadRepositoryEffect(tab.Id, tab.CommandPath))]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[0])!.IsLoading);
        var firstSession = effects.SessionOf(ids[0])!;

        host.Dispatch(new ActivateTabCommand(ids[1]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[1])!.IsLoading);
        Assert.Same(firstSession, effects.SessionOf(ids[0]));
        Assert.Equal(1, firstSession.Graph.Count);

        host.Dispatch(new ActivateTabCommand(ids[2]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[2])!.IsLoading);

        Assert.NotSame(firstSession, effects.SessionOf(ids[0]));
        Assert.True(effects.SessionOf(ids[0])!.IsPending);
        Assert.Equal(0, effects.SessionOf(ids[0])!.Graph.Count);
        Assert.Equal(1, effects.SessionOf(ids[1])!.Graph.Count);

        host.Dispatch(new ActivateTabCommand(ids[0]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[0])!.IsLoading);

        Assert.Equal(1, effects.SessionOf(ids[0])!.Graph.Count);
    }

    [Fact]
    public async Task GivenWatchedTab_WhenGitChangesExternally_ThenGraphAndTabStateFollow()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        var options = new RepositoryWatcherOptions(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(200));
        using var effects = new EffectRunner(TestGit.Services, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel(), watcherOptions: options);
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Status is not null && host.Workspace.FindTab(tabId)!.Worktrees.Length > 0);
        var replaced = 0;
        session.GraphReplaced += () => replaced++;

        repository.Commit("second", new TestFile("b.txt", "2"));
        await pump.RunUntilAsync(() => session.Graph.Count == 2);
        var replacedBeforeTopic = replaced;
        repository.WriteFile("a.txt", "changed");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Status!.IsDirty);
        repository.Git("stash", "push");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Stashes.Length == 1);
        repository.CheckoutNewBranch("topic");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Head!.BranchName == "topic");

        Assert.False(host.Workspace.FindTab(tabId)!.Status!.IsDirty);
        Assert.True(replaced > replacedBeforeTopic);
    }

    [Fact]
    public async Task GivenLoadedTab_WhenRefreshingManually_ThenGraphIsReloaded()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        using var effects = new EffectRunner(TestGit.Services, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel());
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        var replaced = 0;
        session.GraphReplaced += () => replaced++;

        Assert.True(effects.Refresh(tabId));

        await pump.RunUntilAsync(() => replaced == 1);
    }
}
