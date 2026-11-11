using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.App.Loading;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Graph;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.App.Tests.Tabs;

// The cases about the watcher (changes arriving from the file system, background tabs, the baseline racing a change) and the graph
// topology the incremental reload reads keep real temporary repositories. The manual refresh, its failure reporting and applying the
// sections of a result run on fakes of the readers and the refresher.
public class TabRefresherTests
{
    private static readonly RepositoryWatcherOptions FastOptions = new(
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(200),
        UseFileWatchers: true);

    private const RepositoryChangeKinds StatusKinds = RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree;

    private static readonly PollOptions FastPoll = new(
        TimeSpan.FromMilliseconds(40),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromSeconds(1),
        5);

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
    public async Task GivenAStoppedRebase_WhenTheTabIsRefreshed_ThenItsOperationIsReadAndClearsWhenAborted()
    {
        using var repository = TestRepositories.RebaseConflicted();
        using var harness = await Harness.LoadAsync(repository, start: false);
        harness.Refresher.Start();

        await harness.Pump.RunUntilAsync(() => harness.Tab.Operation is not null);

        var info = harness.Tab.Operation!;
        Assert.Equal(RepositoryOperation.Rebase, info.Kind);
        Assert.Equal("other", info.Target);
        Assert.Equal(1, info.Step);
        repository.Git("rebase", "--abort");
        await harness.Pump.RunUntilAsync(() => harness.Tab.Operation is null);
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
    public async Task GivenACutLog_WhenTheTabIsRefreshed_ThenTheGraphStaysCutAtTheSoftLimitWithTheNewTotal()
    {
        using var harness = await FakeHarness.LoadAsync(commits: 10, softRowLimit: 4);
        Assert.Equal(10, harness.Session.TruncatedTotal);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        harness.Refresher.RefreshNow();

        await harness.Pump.RunUntilAsync(() => replaced == 1);
        Assert.Equal(4, harness.Session.Graph.Count);
        Assert.Equal(10, harness.Session.TruncatedTotal);
        Assert.False(harness.Session.IsLoading);
    }

    [Fact]
    public async Task GivenTheRestOfACutLogIsLoading_WhenARefreshIsRequested_ThenItWaitsUntilTheLoadEnds()
    {
        using var harness = await FakeHarness.LoadAsync(commits: 10, softRowLimit: 4);
        harness.Session.ExpandRequested += () => { };
        harness.Session.RequestExpand(true);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        harness.Refresher.RefreshNow();
        Assert.False(harness.Refresher.IsRefreshing);
        Assert.Equal(0, replaced);
        harness.Session.CompleteExpansion();

        await harness.Pump.RunUntilAsync(() => replaced == 1);
    }

    [Fact]
    public async Task GivenARefreshInFlight_WhenTheRefresherIsDisposed_ThenItWaitsForTheCancelledReadToEnd()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Delay = TimeSpan.FromHours(1);
        harness.RepositoryRefresher.WindDown = TimeSpan.FromMilliseconds(300);
        var before = harness.RepositoryRefresher.RefreshCount;
        harness.Refresher.RefreshNow();
        // The read starts on the thread pool and posts nothing while it waits, so the pump has nothing to run.
        Assert.True(SpinWait.SpinUntil(() => harness.RepositoryRefresher.RefreshCount > before, TimeSpan.FromSeconds(10)));

        harness.Refresher.Dispose();
        Assert.True(harness.Refresher.WaitForStop(TimeSpan.FromSeconds(10)));

        Assert.Equal(1, harness.RepositoryRefresher.WoundDownCount);
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
    public async Task GivenSlowAutomaticRefresh_WhenItIsApplied_ThenTheStatusPollIntervalBacksOff()
    {
        using var harness = await FakeHarness.LoadAsync();
        Assert.Equal(FastPoll.StatusInterval, harness.Refresher.StatusPollInterval);
        harness.RepositoryRefresher.Delay = TimeSpan.FromMilliseconds(300);
        var applied = 0;

        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 1);

        Assert.True(harness.Refresher.StatusPollInterval > FastPoll.StatusInterval);
    }

    [Fact]
    public async Task GivenRefreshesThatFail_WhenTheyRepeat_ThenTheStatusPollIntervalGrowsAndASuccessResetsIt()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Failure = new GitException("timed out");
        var applied = 0;

        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 1);
        var afterOne = harness.Refresher.StatusPollInterval;
        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 2);
        var afterTwo = harness.Refresher.StatusPollInterval;
        harness.RepositoryRefresher.Failure = null;
        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 3);

        Assert.True(afterOne > FastPoll.StatusInterval);
        Assert.True(afterTwo > afterOne);
        Assert.Equal(FastPoll.StatusInterval, harness.Refresher.StatusPollInterval);
    }

    [Fact]
    public async Task GivenSlowManualRefresh_WhenItIsApplied_ThenTheStatusPollIntervalStays()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Delay = TimeSpan.FromMilliseconds(300);
        var replaced = 0;
        harness.Session.GraphReplaced += () => replaced++;

        harness.Refresher.RefreshNow();
        await harness.Pump.RunUntilAsync(() => replaced == 1);

        Assert.Equal(FastPoll.StatusInterval, harness.Refresher.StatusPollInterval);
    }

    [Fact]
    public async Task GivenARefreshThatFails_WhenAnotherWasRequestedWhileItRan_ThenThatOneRunsAfterIt()
    {
        using var harness = await FakeHarness.LoadAsync();
        harness.RepositoryRefresher.Failure = new GitException("boom");
        var applied = 0;
        var before = harness.RepositoryRefresher.RefreshCount;

        // The test thread is the pump, so the failed run cannot finish before the second request has been merged.
        harness.Refresher.RefreshNow();
        harness.Refresher.RefreshStatus(() => applied++);
        await harness.Pump.RunUntilAsync(() => applied == 1 && !harness.Refresher.IsRefreshing);

        // Two runs, no more: the retry of a request does not retry itself while the repository keeps failing.
        Assert.Equal(before + 2, harness.RepositoryRefresher.RefreshCount);
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
    public async Task GivenPollingTab_WhenACommitAndAFileEditHappenElsewhere_ThenTheActiveTabShowsBoth()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository, polling: true);
        harness.Refresher.SetActive(true);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);

        repository.Commit("second", new TestFile("b.txt", "2"));
        await harness.Pump.RunUntilAsync(() => harness.Session.Graph.Count == 2);
        repository.WriteFile("a.txt", "edited");

        await harness.Pump.RunUntilAsync(() => harness.Tab.Status!.IsDirty);
    }

    [Fact]
    public async Task GivenDefaultWatcherOptions_WhenTheRepositoryChangesElsewhere_ThenThePollerRunsAndPicksItUp()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        using var harness = await Harness.LoadAsync(repository, watcher: RepositoryWatcherOptions.Default);
        harness.Refresher.SetActive(true);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);

        // The status poll interval is null exactly when file watchers are in use.
        Assert.NotNull(harness.Refresher.StatusPollInterval);
        repository.Commit("second", new TestFile("b.txt", "2"));

        await harness.Pump.RunUntilAsync(() => harness.Session.Graph.Count == 2);
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

    [Fact]
    public async Task GivenOwnRefreshThatSawARefMove_WhenThePollerTicks_ThenNoRefsRefreshFollows()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var counting = new CountingRefresher(TestGit.Services.Refresher);
        using var harness = await Harness.LoadAsync(repository, polling: true, refresher: counting);
        harness.Refresher.SetActive(true);
        await harness.Pump.RunUntilAsync(() => harness.Tab.Status is not null && !harness.Refresher.IsRefreshing);
        repository.Branch("topic");
        var before = counting.Count;

        harness.Refresher.RefreshNow(false);
        await harness.Pump.RunUntilAsync(() => counting.Count > before && !harness.Refresher.IsRefreshing);
        var afterOwn = counting.Count;
        var end = DateTime.UtcNow + TimeSpan.FromMilliseconds(700);
        await harness.Pump.RunUntilAsync(() => DateTime.UtcNow > end);

        Assert.DoesNotContain(counting.KindsFrom(afterOwn), kinds => kinds != StatusKinds);
    }

    private sealed class CountingRefresher(IRepositoryRefresher inner) : IRepositoryRefresher
    {
        private readonly List<RepositoryChangeKinds> _kinds = [];

        public int Count
        {
            get
            {
                lock (_kinds)
                {
                    return _kinds.Count;
                }
            }
        }

        public List<RepositoryChangeKinds> KindsFrom(int index)
        {
            lock (_kinds)
            {
                return [.. _kinds.Skip(index)];
            }
        }

        public Task<RepositoryRefreshResult> RefreshAsync(
            RepositoryLocation location,
            RepositorySnapshot? previous,
            RepositoryChangeKinds kinds,
            CancellationToken cancellationToken)
        {
            lock (_kinds)
            {
                _kinds.Add(kinds);
            }

            return inner.RefreshAsync(location, previous, kinds, cancellationToken);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly Func<int> _runCount;

        private Harness(Dispatcher host, TabId tabId, TabSession session, TabRefresher refresher, UiPump pump, List<GraphReload> reloads, Func<int> runCount, Func<Task> finishLoad)
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

        public Dispatcher Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public static Task<Harness> LoadAsync(
            TestRepository repository,
            bool start = true,
            bool polling = false,
            RepositoryWatcherOptions? watcher = null,
            IRepositoryRefresher? refresher = null) =>
            CreateAsync(repository, start, loadFirst: true, polling, watcher, refresher);

        /// <summary>Starts the refresher while the initial load is still pending; <see cref="FinishLoadAsync"/> completes it.</summary>
        public static Task<Harness> StartBeforeLoadAsync(TestRepository repository) => CreateAsync(repository, start: true, loadFirst: false);

        public void Dispose()
        {
            Refresher.Dispose();
            Session.Dispose();
        }

        private static Task RunInitialLoadAsync(RepositoryLocation location, TabSession session, Dispatcher host, StatusReporter statuses) =>
            TestGit.Services.RepositoryLoader.RunAsync(location, session, host, statuses, InlineUi.Instance, session.Cancellation.Token);

        private static async Task<Harness> CreateAsync(
            TestRepository repository,
            bool start,
            bool loadFirst,
            bool polling = false,
            RepositoryWatcherOptions? watcher = null,
            IRepositoryRefresher? repositoryRefresher = null)
        {
            var location = await TestGit.Services.Discovery.DiscoverAsync(repository.WorkingTreePath, CancellationToken.None);
            var host = new Dispatcher(AppState.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusReporter(host);
            var session = new TabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(TestGit.Services.CommitDetails, location, InlineUi.Instance, TimeSpan.Zero)));
            session.MarkStarted();

            if (loadFirst)
            {
                await RunInitialLoadAsync(location, session, host, statuses);
            }

            var pump = new UiPump(TestContext.Current.CancellationToken);
            var options = watcher ?? (polling ? FastOptions with { UseFileWatchers = false } : FastOptions);
            var reloads = new List<GraphReload>();
            var ran = 0;
            var ui = new DelegateUi(action =>
            {
                pump.Post(() =>
                {
                    action();
                    Interlocked.Increment(ref ran);
                });
            });
            var refresher = new TabRefresher(
                new TabRefreshServices(
                    repositoryRefresher ?? TestGit.Services.Refresher,
                    TestGit.Services.Stashes,
                    TestGit.Services.IncrementalGraphLoader),
                new TabContext(host, session, location, statuses, ui),
                new TabWatching(new WatchBudget(), options, FastPoll));
            refresher.GraphReloaded += reloads.Add;
            if (start)
            {
                refresher.Start();
            }

            return new Harness(host, tabId, session, refresher, pump, reloads, () => Volatile.Read(ref ran), () => RunInitialLoadAsync(location, session, host, statuses));
        }
    }

    private sealed class FakeHarness : IDisposable
    {
        private FakeHarness(Dispatcher host, TabId tabId, TabSession session, TabRefresher refresher, FakeRepositoryRefresher repositoryRefresher, StatusReporter statuses, UiPump pump)
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

        public Dispatcher Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public TabRefresher Refresher { get; }

        public FakeRepositoryRefresher RepositoryRefresher { get; }

        public StatusReporter Statuses { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        /// <summary>Loads a three-commit tab from fakes, starts the refresher and waits until its baseline refresh is done.</summary>
        public static async Task<FakeHarness> LoadAsync(
            int commits = 3,
            int softRowLimit = GraphLoader.DefaultSoftRowLimit)
        {
            var location = FakeStatusReader.LocationOf("refresher");
            var commitLog = FakeCommitLogReader.Chain(commits);
            var refs = new FakeRefReader();
            var stashes = new FakeStashReader();
            var host = new Dispatcher(AppState.Empty);
            host.Dispatch(new OpenRepositoryCommand(location));
            var tabId = host.Workspace.ActiveTabId!.Value;
            var statuses = new StatusReporter(host);
            var session = new TabSession(
                new TabSessionParts(
                    tabId,
                    new CommitDetailsStore(new FakeCommitDetailsReader(), location, InlineUi.Instance, TimeSpan.Zero)));
            session.MarkStarted();
            var graphLoader = new GraphLoader(commitLog, softRowLimit);
            await new RepositoryLoader(refs, new FakeStatusReader(), graphLoader, new SidebarDataLoader(refs, stashes))
                .RunAsync(location, session, host, statuses, InlineUi.Instance, session.Cancellation.Token);
            var pump = new UiPump(TestContext.Current.CancellationToken);
            var repositoryRefresher = new FakeRepositoryRefresher();
            var refresher = new TabRefresher(
                new TabRefreshServices(repositoryRefresher, stashes, new IncrementalGraphLoader(commitLog, graphLoader)),
                new TabContext(host, session, location, statuses, pump),
                new TabWatching(new WatchBudget(), TestGit.QuietWatcherOptions, FastPoll));
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
