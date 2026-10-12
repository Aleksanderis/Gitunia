using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class RepoLoaderTests
{
    [Fact]
    public async Task GivenOpenedTab_WhenLoading_ThenGraphHeadAndRefsArriveAndLoadingEnds()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1")).Commit("second", ("a.txt", "2")).Tag("v1");
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = host.Workspace.ActiveTabId!.Value;
        using var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));
        var changes = 0;
        session.Changed += () => changes++;

        // Posting runs the action inline; the test thread owns the host and session while it awaits the load.
        await RepoLoader.RunAsync(runner, location, session, host, action => action(), session.Cancellation.Token);

        Assert.False(session.IsLoading);
        Assert.Equal(string.Empty, session.LoadError);
        Assert.Equal(2, session.Graph.Count);
        Assert.Equal(2, session.Source.Count);
        Assert.True(changes >= 2);
        var tab = host.Workspace.FindTab(tabId)!;
        Assert.Equal("main", tab.Head!.BranchName);
        Assert.Contains(tab.Refs, reference => reference.Name == "v1");
    }

    [Fact]
    public async Task GivenCancelledTab_WhenLoading_ThenSessionStaysLoadingAndNothingThrows()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        using var session = new TabSession(host.Workspace.ActiveTabId!.Value, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));
        await session.Cancellation.CancelAsync();

        await RepoLoader.RunAsync(runner, location, session, host, action => action(), session.Cancellation.Token);

        Assert.True(session.IsLoading);
        Assert.Equal(0, session.Graph.Count);
    }

    [Fact]
    public async Task GivenDirtyRepository_WhenLoading_ThenTheStatusSummaryArrivesForTheTabBar()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1")).WriteFile("untracked.txt", "x");
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = host.Workspace.ActiveTabId!.Value;
        using var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));

        await RepoLoader.RunAsync(runner, location, session, host, action => action(), session.Cancellation.Token);

        Assert.Equal(new RepoStatusSummary(true, 0, 0), host.Workspace.FindTab(tabId)!.Status);
    }

    [Fact]
    public async Task GivenCleanRepository_WhenLoading_ThenTheStatusSummaryIsNotDirty()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        var runner = new GitRunner();
        var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
        var host = new WorkspaceHost(Workspace.Empty);
        host.Dispatch(new OpenRepositoryCommand(location));
        var tabId = host.Workspace.ActiveTabId!.Value;
        using var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));

        await RepoLoader.RunAsync(runner, location, session, host, action => action(), session.Cancellation.Token);

        Assert.Equal(new RepoStatusSummary(false, 0, 0), host.Workspace.FindTab(tabId)!.Status);
    }
}
