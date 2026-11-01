using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Tags;
using Gitunia.Core.Tags;
using Gitunia.App.Tabs.Models;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests;

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
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(fakes.Services, host, pump.Post, new TabSessionFactory(), new StatusReporter(host), watcherOptions: TestGit.QuietWatcherOptions);

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
    public async Task GivenOpenTabs_WhenCancellingAll_ThenEveryLoadedSessionTokenIsCancelled()
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
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(fakes.Services, host, pump.Post, new TabSessionFactory(), new StatusReporter(host), watcherOptions: TestGit.QuietWatcherOptions);
        host.Restore(new WorkspaceUpdate(
            scratch with { ActiveTabId = firstId },
            [new LoadRepositoryEffect(firstId, scratch.Tabs[0].CommandPath), new LoadRepositoryEffect(secondId, scratch.Tabs[1].CommandPath)]));
        await pump.RunUntilAsync(() => !effects.SessionOf(firstId)!.IsLoading);
        Assert.False(effects.SessionOf(firstId)!.Cancellation.IsCancellationRequested);

        effects.CancelAll();

        Assert.True(effects.SessionOf(firstId)!.Cancellation.IsCancellationRequested);
        Assert.True(effects.SessionOf(secondId)!.Cancellation.IsCancellationRequested);
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
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(
            fakes.Services,
            host,
            pump.Post,
            new TabSessionFactory(),
            new StatusReporter(host),
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
    public async Task GivenColdTabRunningOperation_WhenSwitching_ThenTabIsNotEvicted()
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
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(
            fakes.Services,
            host,
            pump.Post,
            new TabSessionFactory(),
            new StatusReporter(host),
            watcherOptions: TestGit.QuietWatcherOptions,
            warmTabLimit: 2);
        effects.IsTabBusy = tabId => tabId == ids[0];
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

        Assert.Same(firstSession, effects.SessionOf(ids[0]));
        Assert.Equal(1, firstSession.Graph.Count);
    }

    [Fact]
    public async Task GivenColdTabWithAnOperationInState_WhenSwitching_ThenTabIsNotEvictedAndItsWorkIsNotCancelled()
    {
        var fakes = new FakeGitServices();
        var writer = new BlockingTagWriter();
        fakes.CommitLog.AddOnTop("root");
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var scratch = Workspace.Empty;
        foreach (var name in new[] { "first", "second", "third" })
        {
            var location = FakeStatusReader.LocationOf(name);
            scratch = WorkspaceReducer.Reduce(scratch, new OpenRepositoryCommand(location)).Workspace;
        }

        var ids = scratch.Tabs.Select(tab => tab.Id).ToArray();
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(
            fakes.Services with { Tags = writer },
            host,
            pump.Post,
            new TabSessionFactory(),
            new StatusReporter(host),
            watcherOptions: TestGit.QuietWatcherOptions,
            warmTabLimit: 2);
        host.Restore(new WorkspaceUpdate(
            scratch with { ActiveTabId = ids[0] },
            [.. scratch.Tabs.Select(tab => new LoadRepositoryEffect(tab.Id, tab.CommandPath))]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[0])!.IsLoading);
        var firstSession = effects.SessionOf(ids[0])!;
        host.Dispatch(new TagDeleteConfirmed(ids[0], "v1", "1111111111111111111111111111111111111111"));
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.True(host.Workspace.FindTab(ids[0])!.Running.IsBusy);

        host.Dispatch(new ActivateTabCommand(ids[1]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[1])!.IsLoading);
        Assert.Same(firstSession, effects.SessionOf(ids[0]));
        Assert.Equal(1, firstSession.Graph.Count);

        host.Dispatch(new ActivateTabCommand(ids[2]));
        await pump.RunUntilAsync(() => !effects.SessionOf(ids[2])!.IsLoading);

        Assert.Same(firstSession, effects.SessionOf(ids[0]));
        Assert.Equal(1, firstSession.Graph.Count);
        Assert.False(writer.Token.IsCancellationRequested);
        Assert.False(writer.Finished);
    }

    [Fact]
    public async Task GivenPolledTab_WhenGitChangesExternally_ThenGraphAndTabStateFollow()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new Dispatcher(AppState.Empty);
        var interval = TimeSpan.FromMilliseconds(50);
        var poll = new PollOptions(interval, interval, TimeSpan.FromMilliseconds(200), 1);
        using var effects = new EffectRunner<TabSession>(
            TestGit.Services, host, pump.Post, new TabSessionFactory(), new StatusReporter(host), pollOptions: poll);
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
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(TestGit.Services, host, pump.Post, new TabSessionFactory(), new StatusReporter(host));
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        var replaced = 0;
        session.GraphReplaced += () => replaced++;

        Assert.True(effects.Refresh(tabId));

        await pump.RunUntilAsync(() => replaced == 1);
    }

    [Fact]
    public async Task GivenEditedTrackerFile_WhenRefreshingManually_ThenTheNewLinksShow()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var location = await TestGit.Services.Discovery
            .DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
        var update = WorkspaceReducer.Reduce(Workspace.Empty, new OpenRepositoryCommand(location));
        var tabId = update.Workspace.Tabs[0].Id;
        var host = new Dispatcher(AppState.Empty);
        using var effects = new EffectRunner<TabSession>(
            TestGit.Services,
            host,
            pump.Post,
            new TabSessionFactory(),
            new StatusReporter(host));
        host.Restore(update);
        var session = effects.SessionOf(tabId)!;
        await pump.RunUntilAsync(() => !session.IsLoading);
        Assert.True(session.IssueLinks.IsEmpty);
        var trackerFile = Path.Combine(repository.WorkingTreePath, ".issuetracker");

        File.WriteAllText(
            trackerFile,
            "[issuetracker \"x\"]\n\tregex = \"#(\\\\d+)\"\n\turl = https://example.org/$1\n");
        Assert.True(effects.Refresh(tabId));
        await pump.RunUntilAsync(() => !session.IssueLinks.IsEmpty);

        File.Delete(trackerFile);
        Assert.True(effects.Refresh(tabId));
        await pump.RunUntilAsync(() => session.IssueLinks.IsEmpty);
    }
}
