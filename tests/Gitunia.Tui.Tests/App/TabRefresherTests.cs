using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class TabRefresherTests
{
    private static readonly RepositoryWatcherOptions FastOptions = new(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(200));

    [Fact]
    public async Task GivenLoadedTab_WhenTheFirstRefreshRuns_ThenItIsABaselineThatAppliesSectionsWithoutReloadingTheGraph()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository, start: false);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        repository.WriteFile("a.txt", "changed");

        harness.Refresher.Start();

        await WaitUntilAsync(() => harness.Tab.Status!.IsDirty);

        Assert.Equal(0, replaced);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenItSucceeds_ThenTheGraphIsReloadedAndCompletionIsReported()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await WaitUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        harness.Refresher.RefreshNow();

        await WaitUntilAsync(() => replaced == 1);
        Assert.Contains("refreshed", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenGitFails_ThenTheFailureIsReportedEachTime()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await WaitUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var gitDirectory = Path.Combine(repository.Path, ".git");
        var moved = Path.Combine(repository.Path, ".git-moved");
        Directory.Move(gitDirectory, moved);
        try
        {
            harness.Refresher.RefreshNow();
            await WaitUntilAsync(() => harness.Statuses.Log.Latest?.Severity == StatusSeverity.Error && !harness.Refresher.IsRefreshing);
            Assert.Contains("could not refresh", harness.Statuses.Log.Latest!.Text);

            harness.Statuses.Dismiss();
            harness.Refresher.RefreshNow();
            await WaitUntilAsync(() => harness.Statuses.Log.Latest is not null && !harness.Refresher.IsRefreshing);

            Assert.Contains("could not refresh", harness.Statuses.Log.Latest!.Text);
        }
        finally
        {
            Directory.Move(moved, gitDirectory);
        }
    }

    [Fact]
    public async Task GivenBackgroundTab_WhenTheRepositoryChanges_ThenItRefreshesOnActivation()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await WaitUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        harness.Refresher.SetActive(false);

        repository.Commit("second", ("b.txt", "2"));
        await Task.Delay(600, TestContext.Current.CancellationToken);

        Assert.Equal(0, replaced);
        Assert.Equal(1, harness.Session.Graph.Count);

        harness.Refresher.SetActive(true);

        await WaitUntilAsync(() => harness.Session.Graph.Count == 2);
        Assert.Equal(1, replaced);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    private sealed class Harness : IDisposable
    {
        private Harness(WorkspaceHost host, TabId tabId, TabSession session, TabRefresher refresher, StatusChannel statuses)
        {
            Host = host;
            TabId = tabId;
            Session = session;
            Refresher = refresher;
            Statuses = statuses;
        }

        public WorkspaceHost Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public StatusChannel Statuses { get; }

        public RepoState Tab => Host.Workspace.FindTab(TabId)!;

        public static async Task<Harness> LoadAsync(TestRepository repository, bool start = true)
        {
            var runner = new GitRunner();
            var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusChannel();
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repo(runner, location), action => action(), TimeSpan.Zero));
            session.MarkStarted();
            await RepoLoader.RunAsync(runner, location, session, host, statuses, InlineUi.Post, session.Cancellation.Token);
            var refresher = new TabRefresher(runner, host, session, location, statuses, InlineUi.Post, new WatchBudget(), FastOptions);
            if (start)
            {
                refresher.Start();
            }

            return new Harness(host, tabId, session, refresher, statuses);
        }

        public void Dispose()
        {
            Refresher.Dispose();
            Session.Dispose();
        }
    }
}
