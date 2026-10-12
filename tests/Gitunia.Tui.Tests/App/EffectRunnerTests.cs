using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class EffectRunnerTests
{
    [Fact]
    public async Task GivenRestoredTabs_WhenStarting_ThenOnlyActiveTabLoadsUntilActivated()
    {
        using var first = TestRepository.Create();
        first.Commit("root", ("a.txt", "1"));
        using var second = TestRepository.Create();
        second.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var scratch = Workspace.Empty;
        foreach (var path in new[] { first.Path, second.Path })
        {
            var location = await RepositoryDiscovery.DiscoverAsync(runner, path, CancellationToken.None);
            scratch = WorkspaceReducer.Reduce(scratch, new OpenRepositoryCommand(location)).Workspace;
        }

        var firstId = scratch.Tabs[0].Id;
        var secondId = scratch.Tabs[1].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        var effects = new EffectRunner(runner, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel());

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
    public async Task GivenWatchedTab_WhenGitChangesExternally_ThenGraphAndTabStateFollow()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        var options = new RepositoryWatcherOptions(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(200));
        using var effects = new EffectRunner(runner, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel(), watcherOptions: options);
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Status is not null && host.Workspace.FindTab(tabId)!.Worktrees.Length > 0);
        var replaced = 0;
        session.GraphReplaced += () => replaced++;

        repository.Commit("second", ("b.txt", "2"));
        await pump.RunUntilAsync(() => session.Graph.Count == 2);
        repository.WriteFile("a.txt", "changed");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Status!.IsDirty);
        repository.Git("stash", "push");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Stashes.Length == 1);
        repository.CheckoutNewBranch("topic");
        await pump.RunUntilAsync(() => host.Workspace.FindTab(tabId)!.Head!.BranchName == "topic");

        Assert.False(host.Workspace.FindTab(tabId)!.Status!.IsDirty);
        Assert.Equal(2, replaced);
    }

    [Fact]
    public async Task GivenLoadedTab_WhenRefreshingManually_ThenGraphIsReloaded()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new WorkspaceHost(Workspace.Empty);
        using var effects = new EffectRunner(runner, host, pump.Post, GraphGlyphSet.Unicode, new StatusChannel());
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        var replaced = 0;
        session.GraphReplaced += () => replaced++;

        Assert.True(effects.Refresh(tabId));

        await pump.RunUntilAsync(() => replaced == 1);
    }
}
