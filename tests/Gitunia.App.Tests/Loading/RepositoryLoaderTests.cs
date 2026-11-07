using Gitunia.App.Status;
using Gitunia.Core;
using Gitunia.App.Tabs;
using Gitunia.App.Loading;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Xunit;

namespace Gitunia.App.Tests.Loading;

// Every test here runs on fakes of the readers: what is under test is how the loader hands results to the UI and reports failures,
// not git. The wiring against real git is covered by EffectRunnerTests and TabRefresherTests.
public class RepositoryLoaderTests
{
    [Fact]
    public async Task GivenOpenedTab_WhenLoading_ThenGraphHeadAndRefsArriveAndLoadingEnds()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");
        harness.CommitLog.AddOnTop("second");
        harness.Refs.Refs.Add(new GitRef("refs/tags/v1", "v1", RefKind.Tag, "1", "1", null, null, null, false, false, null, null));
        var changes = 0;
        harness.Session.Changed += () => changes++;

        // Posts run inline but serialized by InlineUi, standing in for the single UI thread.
        await harness.RunAsync();

        Assert.False(harness.Session.IsLoading);
        Assert.Equal(string.Empty, harness.Session.LoadError);
        Assert.Equal(2, harness.Session.Graph.Count);
        Assert.True(changes >= 2);
        Assert.Equal("main", harness.Tab.Head!.BranchName);
        Assert.Contains(harness.Tab.Refs, reference => reference.Name == "v1");
    }

    [Fact]
    public async Task GivenCancelledTab_WhenLoading_ThenSessionStaysLoadingAndNothingThrows()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");
        await harness.Session.Cancellation.CancelAsync();

        await harness.RunAsync();

        Assert.Empty(harness.Statuses.Log.History);
        Assert.True(harness.Session.IsLoading);
        Assert.Equal(0, harness.Session.Graph.Count);
    }

    [Fact]
    public async Task GivenDirtyRepository_WhenLoading_ThenTheStatusSummaryArrivesForTheTabBar()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");
        harness.Status.SetDirty(harness.Location, true);

        await harness.RunAsync();

        Assert.Equal(new RepositoryStatusSummary(true, 0, 0), harness.Tab.Status);
    }

    [Fact]
    public async Task GivenANewerStatusAlreadyApplied_WhenTheLoadsOlderReadArrives_ThenTheNewerStatusStays()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");
        harness.Status.SetDirty(harness.Location, true);
        var newer = await harness.Status.ReadStatusAsync(
            harness.Location,
            false,
            UntrackedFilesMode.All,
            TestContext.Current.CancellationToken);
        harness.Host.Dispatch(new TabCommand(harness.TabId, new WorkingTreeLoadedCommand(newer)));
        harness.Status.SetDirty(harness.Location, false);

        await harness.RunAsync();

        Assert.Equal(new RepositoryStatusSummary(true, 0, 0), harness.Tab.Status);
        Assert.Single(harness.Tab.WorkingTree!.Snapshot.Entries);
    }

    [Fact]
    public async Task GivenCleanRepository_WhenLoading_ThenTheStatusSummaryIsNotDirty()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");

        await harness.RunAsync();

        Assert.Equal(new RepositoryStatusSummary(false, 0, 0), harness.Tab.Status);
    }

    [Fact]
    public async Task GivenRepositoryThatVanished_WhenLoading_ThenTheFailedReadsAreReportedToTheStatusChannel()
    {
        using var harness = new Harness();
        var failure = new GitException("not a git repository");
        harness.Refs.WorktreesFailure = failure;
        harness.Stashes.Failure = failure;
        harness.CommitLog.Failure = failure;

        await harness.RunAsync();

        var texts = harness.Statuses.Log.History.Select(message => message.Text).ToList();
        Assert.Contains(texts, text => text.Contains("could not read the worktrees", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains("could not read the stashes", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.Contains(": could not load the graph:", StringComparison.Ordinal));
        Assert.NotEqual(string.Empty, harness.Session.LoadError);
        Assert.All(harness.Statuses.Log.History, message => Assert.Equal(StatusSeverity.Error, message.Severity));
    }

    [Fact]
    public async Task GivenHeadReadFails_WhenLoading_ThenTheStatusReadFailureIsReportedAndTheGraphStillLoads()
    {
        using var harness = new Harness();
        harness.CommitLog.AddOnTop("root");
        harness.Refs.HeadFailure = new GitException("boom");

        await harness.RunAsync();

        var texts = harness.Statuses.Log.History.Select(message => message.Text).ToList();
        Assert.Contains(texts, text => text.Contains("could not read the repository status: boom", StringComparison.Ordinal));
        Assert.Equal(1, harness.Session.Graph.Count);
        Assert.False(harness.Session.IsLoading);
    }

    [Fact]
    public async Task GivenLogLongerThanTheSoftLimit_WhenLoading_ThenOnlyTheNewestRowsLoadAndTheSessionIsTruncated()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);

        await harness.RunAsync();

        Assert.Equal(4, harness.Session.Graph.Count);
        Assert.Equal(10, harness.Session.TruncatedTotal);
        Assert.False(harness.Session.IsLoading);
        Assert.False(harness.Session.IsGraphComplete);
        Assert.Equal("commit 10", harness.Session.Graph[0].Subject);
    }

    [Fact]
    public async Task GivenLogWithinTheSoftLimit_WhenLoading_ThenTheSessionIsNotTruncated()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 4);

        await harness.RunAsync();

        Assert.Equal(4, harness.Session.Graph.Count);
        Assert.Null(harness.Session.TruncatedTotal);
        Assert.True(harness.Session.IsGraphComplete);
    }

    [Fact]
    public async Task GivenTruncatedSession_WhenRequestingTheRest_ThenTheRowsAreAppendedAndTheGraphIsComplete()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);
        await harness.RunAsync();

        harness.Session.RequestExpand(true);
        Assert.True(harness.Session.IsLoading);
        await harness.ExpandAsync();

        Assert.Equal(10, harness.Session.Graph.Count);
        var graph = harness.Session.Graph;
        var shownIds = Enumerable.Range(0, 10).Select(index => graph[index].Id);
        Assert.Equal(harness.CommitLog.Commits.Select(commit => commit.Id), shownIds);
        Assert.True(harness.Session.IsGraphComplete);
        Assert.Null(harness.Session.TruncatedTotal);
    }

    [Fact]
    public async Task GivenCompleteSession_WhenRequestingTheRest_ThenNothingHappens()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 4);
        await harness.RunAsync();
        var requests = 0;
        harness.Session.ExpandRequested += () => requests++;

        harness.Session.RequestExpand(true);

        Assert.Equal(0, requests);
        Assert.False(harness.Session.IsLoading);
    }

    [Fact]
    public async Task GivenRestIsLoading_WhenRequestingAgain_ThenNoSecondLoadStarts()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);
        await harness.RunAsync();
        var requests = 0;
        harness.Session.ExpandRequested += () => requests++;

        harness.Session.RequestExpand();
        harness.Session.RequestExpand(true);

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task GivenGraphReplacedWhileTheRestLoads_WhenTheStaleReadArrives_ThenItAddsNothing()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);
        await harness.RunAsync();

        var shown = harness.Session.Graph.Count;
        var staleVersion = harness.Session.GraphVersion;
        var graph = harness.Session.Graph;
        harness.Session.ReplaceGraph([.. Enumerable.Range(0, shown).Select(index => graph[index])], 10);

        await harness.Loader.ExpandAsync(
            harness.Location,
            harness.Session,
            harness.Statuses,
            InlineUi.Instance,
            shown,
            harness.Session.Graph[shown - 1].Id,
            staleVersion,
            CancellationToken.None);

        Assert.Equal(4, harness.Session.Graph.Count);
        Assert.Equal(10, harness.Session.TruncatedTotal);
    }

    [Fact]
    public async Task GivenHistoryChangedUnderTheShownRows_WhenTheRestLoads_ThenNothingIsAppendedAndItIsReported()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);
        await harness.RunAsync();
        harness.CommitLog.AddOnTop("new top");
        harness.Session.RequestExpand(true);

        await harness.ExpandAsync();

        Assert.Equal(4, harness.Session.Graph.Count);
        Assert.True(harness.Session.IsTruncated);
        Assert.True(harness.Session.ExpansionFailed);
        Assert.False(harness.Session.IsLoading);
        var texts = harness.Statuses.Log.History.Select(message => message.Text);
        Assert.Contains(texts, text => text.Contains("history changed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenFailedRestLoad_WhenRequestingWithoutTheUser_ThenItDoesNotRetryAndAUserActionDoes()
    {
        using var harness = new Harness(softRowLimit: 4);
        AddCommits(harness, 10);
        await harness.RunAsync();
        harness.Session.RequestExpand(true);
        harness.CommitLog.Failure = new GitException("boom");
        await harness.ExpandAsync();
        var requests = 0;
        harness.Session.ExpandRequested += () => requests++;

        harness.Session.RequestExpand();
        Assert.Equal(0, requests);
        harness.Session.RequestExpand(true);

        Assert.Equal(1, requests);
    }

    private static void AddCommits(Harness harness, int count)
    {
        for (var number = 1; number <= count; number++)
        {
            harness.CommitLog.AddOnTop($"commit {number}");
        }
    }

    private sealed class Harness : IDisposable
    {
        public Harness(int softRowLimit = GraphLoader.DefaultSoftRowLimit)
        {
            Location = FakeStatusReader.LocationOf("loader");
            Host = new Dispatcher(AppState.Empty);
            Statuses = new StatusReporter(Host);
            Host.Dispatch(new OpenRepositoryCommand(Location));
            TabId = Host.Workspace.ActiveTabId!.Value;
            Session = new TabSession(
                new TabSessionParts(
                    TabId,
                    new CommitDetailsStore(new FakeCommitDetailsReader(), Location, InlineUi.Instance, TimeSpan.Zero)));
            var graphLoader = new GraphLoader(CommitLog, softRowLimit);
            Loader = new RepositoryLoader(Refs, Status, graphLoader, new SidebarDataLoader(Refs, Stashes));
            Session.ExpandRequested += OnExpandRequested;
        }

        public RepositoryLocation Location { get; }

        public FakeCommitLogReader CommitLog { get; } = new();

        public FakeRefReader Refs { get; } = new();

        public FakeStatusReader Status { get; } = new();

        public FakeStashReader Stashes { get; } = new();

        public StatusReporter Statuses { get; }

        public Dispatcher Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        public RepositoryLoader Loader { get; }

        public Task RunAsync() => Loader.RunAsync(Location, Session, Host, Statuses, InlineUi.Instance, Session.Cancellation.Token);

        // Stands in for the effect runner's answer to a request for the rest of the log.
        public Task ExpandAsync()
        {
            var shown = Session.Graph.Count;

            return Loader.ExpandAsync(
                Location,
                Session,
                Statuses,
                InlineUi.Instance,
                shown,
                Session.Graph[shown - 1].Id,
                Session.GraphVersion,
                Session.Cancellation.Token);
        }

        public void Dispose() => Session.Dispose();

        private void OnExpandRequested()
        {
        }
    }
}
