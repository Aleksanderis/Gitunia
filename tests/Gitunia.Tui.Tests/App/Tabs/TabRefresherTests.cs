using Gitunia.Git.Repositories.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs;

// The cases about the watcher (changes arriving from the file system, background tabs, the baseline racing a change) and the graph
// topology the incremental reload reads keep real temporary repositories. The manual refresh, its failure reporting and applying the
// sections of a result run on fakes of the readers and the refresher.
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
    public async Task GivenAModifiedFile_WhenItIsEditedAgain_ThenTheSessionHearsThatTheStatusWasReadWithoutAChange()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.WriteFile("a.txt", "changed");
        using var harness = await Harness.LoadAsync(repository, start: false);
        harness.Refresher.Start();
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status!.IsDirty);
        var heard = 0;
        harness.Session.StatusRead += () => heard++;

        repository.WriteFile("a.txt", "changed twice");

        await harness.Pump.RunUntilAsync(() => heard > 0);
        Assert.Equal(1, heard);
    }

    [Fact]
    public async Task GivenManualRefresh_WhenItSucceeds_ThenTheGraphIsReloadedAndCompletionIsReported()
    {
        using var harness = await FakeHarness.LoadAsync();
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
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Failure = new GitException("not a git repository");

        harness.Refresher.RefreshNow();
        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest?.Severity == StatusSeverity.Error && !harness.Refresher.IsRefreshing);
        Assert.Contains("could not refresh", harness.Statuses.Log.Latest!.Text);

        harness.Statuses.Dismiss();
        harness.Refresher.RefreshNow();
        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest is not null && !harness.Refresher.IsRefreshing);

        Assert.Contains("could not refresh", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenFailedRefresh_WhenTheNextOneSucceeds_ThenTheChangedSectionsAreApplied()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Failure = new GitException("boom");
        harness.Refresher.RefreshNow();
        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest?.Severity == StatusSeverity.Error && !harness.Refresher.IsRefreshing);
        harness.RepositoryRefresher.Failure = null;
        harness.RepositoryRefresher.Snapshot = harness.RepositoryRefresher.Snapshot with { Head = new HeadState(HeadKind.Branch, "topic", null) };

        harness.Refresher.RefreshNow();

        await harness.Pump.RunUntilAsync(() => harness.Tab.Head!.BranchName == "topic");
    }

    [Fact]
    public async Task GivenStatusRefreshRequest_WhenTheReadIsApplied_ThenTheCallbackRunsOnceWithoutReloadingTheGraph()
    {
        using var harness = await FakeHarness.LoadAsync();
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;
        var applied = 0;

        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 1 && !harness.Refresher.IsRefreshing);

        Assert.Equal(0, replaced);
        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task GivenStatusRefreshRequest_WhenTheReadFails_ThenTheCallbackStillRuns()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Failure = new GitException("boom");
        var applied = 0;

        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 1);

        Assert.Equal(1, applied);
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
        private readonly Func<int> _runCount;

        private Harness(WorkspaceHost host, TabId tabId, TabSession session, TabRefresher refresher, UiPump pump, List<GraphReload> reloads, Func<int> runCount, Func<Task> finishLoad)
        {
            _runCount = runCount;
            FinishLoadAsync = finishLoad;
            Reloads = reloads;
            Pump = pump;
            Host = host;
            TabId = tabId;
            Session = session;
            Refresher = refresher;
        }

        public UiPump Pump { get; }

        /// <summary>Number of callbacks the refresher posted that the pump has already run (watcher events applied before the load finished). Counted after the run, not at post time: a posted callback still queued has not told the refresher about the change yet.</summary>
        public int RunCount => _runCount();

        public Func<Task> FinishLoadAsync { get; }

        public List<GraphReload> Reloads { get; }

        public WorkspaceHost Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public static Task<Harness> LoadAsync(TestRepository repository, bool start = true) => CreateAsync(repository, start, loadFirst: true);

        /// <summary>Starts the refresher while the initial load is still pending; <see cref="FinishLoadAsync"/> completes it.</summary>
        public static Task<Harness> StartBeforeLoadAsync(TestRepository repository) => CreateAsync(repository, start: true, loadFirst: false);

        public void Dispose()
        {
            Refresher.Dispose();
            Session.Dispose();
        }

        private static Task RunInitialLoadAsync(RepositoryLocation location, TabSession session, WorkspaceHost host, StatusChannel statuses) =>
            TestGit.Services.RepositoryLoader.RunAsync(location, session, host, statuses, InlineUi.Post, session.Cancellation.Token);

        private static async Task<Harness> CreateAsync(TestRepository repository, bool start, bool loadFirst)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusChannel();
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(TestGit.Services.CommitDetails, location, action => action(), TimeSpan.Zero));
            session.MarkStarted();

            if (loadFirst)
            {
                await RunInitialLoadAsync(location, session, host, statuses);
            }

            var pump = new UiPump(TestContext.Current.CancellationToken);
            var reloads = new List<GraphReload>();
            var ran = 0;
            var refresher = new TabRefresher(TestGit.Services.Refresher, TestGit.Services.Stashes, TestGit.Services.IncrementalGraphLoader, host, session, location, statuses, action =>
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

            return new Harness(host, tabId, session, refresher, pump, reloads, () => Volatile.Read(ref ran), () => RunInitialLoadAsync(location, session, host, statuses));
        }
    }

    private sealed class FakeHarness : IDisposable
    {
        private FakeHarness(WorkspaceHost host, TabId tabId, TabSession session, TabRefresher refresher, FakeRepositoryRefresher repositoryRefresher, StatusChannel statuses, UiPump pump)
        {
            Host = host;
            TabId = tabId;
            Session = session;
            Refresher = refresher;
            RepositoryRefresher = repositoryRefresher;
            Statuses = statuses;
            Pump = pump;
        }

        public UiPump Pump { get; }

        public WorkspaceHost Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public FakeRepositoryRefresher RepositoryRefresher { get; }

        public StatusChannel Statuses { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        /// <summary>Loads a three-commit tab from fakes, starts the refresher and waits until its baseline refresh is done.</summary>
        public static async Task<FakeHarness> LoadAsync()
        {
            var location = FakeStatusReader.LocationOf("refresher");
            var commitLog = FakeCommitLogReader.Chain(3);
            var refs = new FakeRefReader();
            var stashes = new FakeStashReader();
            var host = new WorkspaceHost(Workspace.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusChannel();
            var session = new TabSession(tabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new FakeCommitDetailsReader(), location, action => action(), TimeSpan.Zero));
            session.MarkStarted();
            var graphLoader = new GraphLoader(commitLog);
            await new RepositoryLoader(refs, new FakeStatusReader(), graphLoader, new SidebarDataLoader(refs, stashes))
                .RunAsync(location, session, host, statuses, InlineUi.Post, session.Cancellation.Token);
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var repositoryRefresher = new FakeRepositoryRefresher();
            var refresher = new TabRefresher(repositoryRefresher, stashes, new IncrementalGraphLoader(commitLog, graphLoader), host, session, location, statuses, pump.Post, new WatchBudget(), TestGit.QuietWatcherOptions);
            refresher.Start();
            await pump.RunUntilAsync(() => repositoryRefresher.RefreshCount > 0 && !refresher.IsRefreshing);
            return new FakeHarness(host, tabId, session, refresher, repositoryRefresher, statuses, pump);
        }

        public void Dispose()
        {
            Refresher.Dispose();
            Session.Dispose();
        }
    }
}
