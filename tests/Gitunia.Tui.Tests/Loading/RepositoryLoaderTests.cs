using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Tests.App;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

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
        Assert.Equal(2, harness.Session.Source.Count);
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

    private sealed class Harness : IDisposable
    {
        public Harness()
        {
            Location = FakeStatusReader.LocationOf("loader");
            Host = new WorkspaceHost(Workspace.Empty);
            Host.Dispatch(new OpenRepositoryCommand(Location));
            TabId = Host.Workspace.ActiveTabId!.Value;
            Session = new TabSession(TabId, GraphGlyphSet.Unicode, new CommitDetailsStore(new FakeCommitDetailsReader(), Location, action => action(), TimeSpan.Zero));
            Loader = new RepositoryLoader(Refs, Status, new GraphLoader(CommitLog), new SidebarDataLoader(Refs, Stashes));
        }

        public RepositoryLocation Location { get; }

        public FakeCommitLogReader CommitLog { get; } = new();

        public FakeRefReader Refs { get; } = new();

        public FakeStatusReader Status { get; } = new();

        public FakeStashReader Stashes { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public WorkspaceHost Host { get; }

        public TabId TabId { get; }

        public TabSession Session { get; }

        public RepositoryState Tab => Host.Workspace.FindTab(TabId)!;

        private RepositoryLoader Loader { get; }

        public Task RunAsync() => Loader.RunAsync(Location, Session, Host, Statuses, InlineUi.Post, Session.Cancellation.Token);

        public void Dispose() => Session.Dispose();
    }
}
