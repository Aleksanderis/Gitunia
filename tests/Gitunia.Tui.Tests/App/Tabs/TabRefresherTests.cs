using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Rendering.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

public class TabRefresherTests
{
    private static readonly RepositoryWatcherOptions FastOptions = new(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(200));

    [Fact]
    public async Task GivenLoadedTab_WhenTheFirstRefreshRuns_ThenItIsABaselineThatAppliesSectionsWithoutReloadingTheGraph()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository, start: false);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        repository.WriteFile("a.txt", "changed");

        harness.Refresher.Start();

        await harness.Pump.RunUntilAsync(() => harness.Tab.Status!.IsDirty);

        Assert.Equal(0, replaced);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenItSucceeds_ThenTheGraphIsReloadedAndCompletionIsReported()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        harness.Refresher.RefreshNow();

        await harness.Pump.RunUntilAsync(() => replaced == 1);
        Assert.Contains("refreshed", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenGitFails_ThenTheFailureIsReportedEachTime()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var gitDirectory = Path.Combine(repository.Path, ".git");
        var moved = Path.Combine(repository.Path, ".git-moved");
        Directory.Move(gitDirectory, moved);
        try
        {
            harness.Refresher.RefreshNow();
            await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest?.Severity == StatusSeverity.Error && !harness.Refresher.IsRefreshing);
            Assert.Contains("could not refresh", harness.Statuses.Log.Latest!.Text);

            harness.Statuses.Dismiss();
            harness.Refresher.RefreshNow();
            await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest is not null && !harness.Refresher.IsRefreshing);

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
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        harness.Refresher.SetActive(false);

        repository.Commit("second", new TestFile("b.txt", "2"));

        // That a hidden watcher stays silent is covered at the watcher level (RepositoryWatcherTests); waiting for silence here would need a sleep.
        // Whether the events arrived before or after activation, the tab ends up refreshing, so this only needs the pump.
        harness.Refresher.SetActive(true);

        await harness.Pump.RunUntilAsync(() => harness.Session.Graph.Count == 2);
        Assert.Equal(1, replaced);
    }

    [Fact]
    public async Task GivenQuietTab_WhenACommitArrivesAutomatically_ThenTheGraphReloadReusesTheOldRows()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Commit("second", new TestFile("b.txt", "2"));
        repository.Commit("third", new TestFile("c.txt", "3"));
        repository.Commit("fourth", new TestFile("d.txt", "4"));
        using var harness = await Harness.LoadAsync(repository);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);

        repository.Commit("fifth", new TestFile("e.txt", "5"));

        await harness.Pump.RunUntilAsync(() => harness.Reloads.Count == 1);
        Assert.True(harness.Reloads[0].ReusedCount > 0);
        Assert.Equal(5, harness.Session.Graph.Count);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenItReloadsTheGraph_ThenNoRowIsReused()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Commit("second", new TestFile("b.txt", "2"));
        using var harness = await Harness.LoadAsync(repository);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);

        harness.Refresher.RefreshNow();

        await harness.Pump.RunUntilAsync(() => harness.Reloads.Count == 1);
        Assert.Equal(0, harness.Reloads[0].ReusedCount);
    }

    [Fact]
    public async Task GivenChangeBeforeTheBaseline_WhenTheBaselineRuns_ThenTheGraphIsReadInFull()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Commit("second", new TestFile("b.txt", "2"));
        repository.Commit("third", new TestFile("c.txt", "3"));
        repository.Commit("fourth", new TestFile("d.txt", "4"));
        using var harness = await Harness.StartBeforeLoadAsync(repository);
        repository.Commit("fifth", new TestFile("e.txt", "5"));
        await harness.Pump.RunUntilAsync(() => harness.RunCount > 0);

        await harness.FinishLoadAsync();

        await harness.Pump.RunUntilAsync(() => harness.Reloads.Count == 1);
        Assert.Equal(0, harness.Reloads[0].ReusedCount);
        Assert.Equal(5, harness.Session.Graph.Count);
    }

    [Fact]
    public async Task GivenCommitBetweenTheLoadAndTheBaselineRead_WhenTheBaselineRuns_ThenTheGraphIsReloadedIncrementally()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Commit("second", new TestFile("b.txt", "2"));
        repository.Commit("third", new TestFile("c.txt", "3"));
        using var harness = await Harness.LoadAsync(repository, start: false);
        repository.Commit("fourth", new TestFile("d.txt", "4"));

        harness.Refresher.Start();

        await harness.Pump.RunUntilAsync(() => harness.Reloads.Count == 1);
        Assert.True(harness.Reloads[0].ReusedCount > 0);
        Assert.Equal(4, harness.Session.Graph.Count);
    }

    [Fact]
    public async Task GivenBranchCreatedBetweenTheLoadAndTheBaselineRead_WhenTheBaselineRuns_ThenTheGraphShowsIt()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Commit("second", new TestFile("b.txt", "2"));
        using var harness = await Harness.LoadAsync(repository, start: false);
        repository.Git("branch", "topic", "HEAD~1");

        harness.Refresher.Start();

        await harness.Pump.RunUntilAsync(() => harness.Reloads.Count == 1);
        Assert.Contains(harness.Session.Graph.ToArray(), entry => entry.Decorations.Any(decoration => decoration.Name == "topic"));
    }

    private sealed class Harness : IDisposable
    {
        private readonly Func<int> runCount;

        private Harness(WorkspaceHost host, TabId tabId, TabSession session, TabRefresher refresher, StatusChannel statuses, UiPump pump, List<GraphReload> reloads, Func<int> runCount, Func<Task> finishLoad)
        {
            this.runCount = runCount;
            FinishLoadAsync = finishLoad;
            Reloads = reloads;
            Pump = pump;
            Host = host;
            TabId = tabId;
            Session = session;
            Refresher = refresher;
            Statuses = statuses;
        }

        public UiPump Pump { get; }

        /// <summary>Number of callbacks the refresher posted that the pump has already run (watcher events applied before the load finished). Counted after the run, not at post time: a posted callback still queued has not told the refresher about the change yet.</summary>
        public int RunCount => runCount();

        public Func<Task> FinishLoadAsync { get; }

        public List<GraphReload> Reloads { get; }

        public WorkspaceHost Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public StatusChannel Statuses { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public static Task<Harness> LoadAsync(TestRepository repository, bool start = true) => CreateAsync(repository, start, loadFirst: true);

        /// <summary>Starts the refresher while the initial load is still pending; <see cref="FinishLoadAsync"/> completes it.</summary>
        public static Task<Harness> StartBeforeLoadAsync(TestRepository repository) => CreateAsync(repository, start: true, loadFirst: false);

        public void Dispose()
        {
            Refresher.Dispose();
            Session.Dispose();
        }

        private static async Task<Harness> CreateAsync(TestRepository repository, bool start, bool loadFirst)
        {
            var runner = new GitRunner();
            var location = await RepositoryDiscovery.DiscoverAsync(runner, repository.Path, CancellationToken.None);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusChannel();
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new Repository(runner, location), action => action(), TimeSpan.Zero));
            session.MarkStarted();
            Task LoadAsync() => RepositoryLoader.RunAsync(runner, location, session, host, statuses, InlineUi.Post, session.Cancellation.Token);
            if (loadFirst)
            {
                await LoadAsync();
            }

            var pump = new UiPump(TestContext.Current.CancellationToken);
            var reloads = new List<GraphReload>();
            var ran = 0;
            var refresher = new TabRefresher(runner, host, session, location, statuses, action =>
            {
                pump.Post(() =>
                {
                    action();
                    Interlocked.Increment(ref ran);
                });
            }, new WatchBudget(), FastOptions, reloads.Add);
            if (start)
            {
                refresher.Start();
            }

            return new Harness(host, tabId, session, refresher, statuses, pump, reloads, () => Volatile.Read(ref ran), LoadAsync);
        }
    }
}
