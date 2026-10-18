using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

public class RemoteActionsTests
{
    [Fact]
    public async Task GivenAFetch_WhenItFinishes_ThenItRanOnTheTabsRepositoryAndTheTabRefreshed()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.RemoteFetch);
        await harness.WaitAsync();

        Assert.Equal([$"fetch|{harness.Root}|all=False|prune=True"], harness.Writer.Calls);
        Assert.Equal("Fetched the remote of the current branch", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Info, harness.Statuses.Log.Latest.Severity);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Actions.IsRunning(harness.TabId));
    }

    [Fact]
    public async Task GivenFetchAll_WhenRunning_ThenEveryRemoteIsRequested()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.RemoteFetchAll);
        await harness.WaitAsync();

        Assert.Equal([$"fetch|{harness.Root}|all=True|prune=True"], harness.Writer.Calls);
        Assert.Equal("Fetched all remotes", harness.Statuses.Log.Latest!.Text);
    }

    [Theory]
    [InlineData(PullOutcome.UpToDate, "Already up to date")]
    [InlineData(PullOutcome.FastForwarded, "Pulled (fast-forward)")]
    public async Task GivenAPull_WhenItFinishes_ThenTheOutcomeIsReportedAndTheGateIsFree(
        PullOutcome outcome,
        string text)
    {
        using var harness = new Harness();
        harness.Writer.Outcome = outcome;

        harness.Execute(ActionIds.RemotePull);
        Assert.True(harness.Gate.IsBusy(harness.TabId));
        await harness.WaitAsync();

        Assert.Equal([$"pull|{harness.Root}"], harness.Writer.Calls);
        Assert.Equal(text, harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.Gate.IsAnyBusy);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenADivergedPull_WhenItFails_ThenTheErrorSaysSoAndTheTabStillRefreshes()
    {
        using var harness = new Harness();
        harness.Writer.Failure = new PullDivergedException("fatal: Not possible to fast-forward");

        harness.Execute(ActionIds.RemotePull);
        await harness.WaitAsync();

        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal(StatusSeverity.Error, latest.Severity);
        Assert.StartsWith(
            "Pull failed: Cannot fast-forward: local and remote have diverged",
            latest.Text,
            StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public async Task GivenAPush_WhenGitRefuses_ThenTheErrorShowsGitsFirstLine()
    {
        using var harness = new Harness();
        harness.Writer.Failure = new GitCommandFailedException(["push"], 1, "! [rejected] main -> main\nhint: more");

        harness.Execute(ActionIds.RemotePush);
        await harness.WaitAsync();

        Assert.Equal([$"push|{harness.Root}|remote=(default)"], harness.Writer.Calls);
        Assert.Equal("Push failed: ! [rejected] main -> main", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest.Severity);
    }

    [Fact]
    public async Task GivenProgress_WhenRunning_ThenTheLineShowsGitsLatestReportAndNoMessageIsPosted()
    {
        using var harness = new Harness();
        harness.Writer.Blocks = true;
        harness.Writer.Reports.Add(new RemoteProgress("Receiving objects", 45));

        harness.Execute(ActionIds.RemoteFetch);
        Assert.Equal("Fetching...", harness.Actions.ProgressLine(harness.TabId));
        await harness.Writer.Started;
        await harness.Pump.RunUntilAsync(
            () => harness.Actions.ProgressLine(harness.TabId).Contains('%', StringComparison.Ordinal));

        Assert.Equal("Fetching: Receiving objects 45%", harness.Actions.ProgressLine(harness.TabId));
        Assert.True(harness.Actions.IsRunning(harness.TabId));
        Assert.Null(harness.Statuses.Log.Latest);

        harness.Writer.Release();
        await harness.WaitAsync();

        Assert.Equal(string.Empty, harness.Actions.ProgressLine(harness.TabId));
    }

    [Fact]
    public async Task GivenARunningFetch_WhenEscIsPressed_ThenItIsCancelledWarnedAndTheTabRefreshes()
    {
        using var harness = new Harness();
        harness.Writer.Blocks = true;
        harness.Execute(ActionIds.RemoteFetch);
        await harness.Writer.Started;

        var claimed = harness.Actions.TryExecute(ActionIds.Cancel, null);
        await harness.WaitAsync();

        Assert.True(claimed);
        var latest = harness.Statuses.Log.Latest!;
        Assert.Equal("Fetch cancelled", latest.Text);
        Assert.Equal(StatusSeverity.Warning, latest.Severity);
        Assert.Equal(1, harness.Refreshes);
        Assert.False(harness.Actions.IsRunning(harness.TabId));
    }

    [Fact]
    public async Task GivenARunningFetch_WhenTheCancelActionRuns_ThenItIsCancelledFromAnyPane()
    {
        using var harness = new Harness();
        harness.Writer.Blocks = true;
        harness.Execute(ActionIds.RemoteFetch);
        await harness.Writer.Started;

        harness.Execute(ActionIds.RemoteCancel);
        await harness.WaitAsync();

        Assert.Equal("Fetch cancelled", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenNothingRunning_WhenTheCancelActionRuns_ThenItSaysSo()
    {
        using var harness = new Harness();

        harness.Execute(ActionIds.RemoteCancel);

        Assert.Equal("No fetch, pull or push is running", harness.Actions.Notice);
    }

    [Fact]
    public async Task GivenARunningPull_WhenCancelling_ThenTheGateIsReleased()
    {
        using var harness = new Harness();
        harness.Writer.Blocks = true;
        harness.Execute(ActionIds.RemotePull);
        await harness.Writer.Started;

        harness.Actions.TryExecute(ActionIds.Cancel, null);
        await harness.WaitAsync();

        Assert.Equal("Pull cancelled", harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.Gate.IsAnyBusy);
    }

    [Fact]
    public void GivenNothingRunning_WhenEscIsPressed_ThenItIsLeftToOthers()
    {
        using var harness = new Harness();

        var claimed = harness.Actions.TryExecute(ActionIds.Cancel, null);

        Assert.False(claimed);
        Assert.Empty(harness.Writer.Calls);
    }

    [Fact]
    public async Task GivenARunningFetch_WhenPushing_ThenItIsRefusedAsBusyAndGitRunsOnce()
    {
        using var harness = new Harness();
        harness.Writer.Blocks = true;
        harness.Execute(ActionIds.RemoteFetch);
        await harness.Writer.Started;

        harness.Execute(ActionIds.RemotePush);

        Assert.StartsWith("Busy:", harness.Actions.Notice, StringComparison.Ordinal);
        Assert.Single(harness.Writer.Calls);

        harness.Writer.Release();
        await harness.WaitAsync();
    }

    [Fact]
    public void GivenAnotherOperationOnTheTab_WhenPulling_ThenItIsRefusedWithoutGit()
    {
        using var harness = new Harness();
        harness.Gate.Begin(harness.TabId);

        harness.Execute(ActionIds.RemotePull);

        Assert.StartsWith("Busy:", harness.Actions.Notice, StringComparison.Ordinal);
        Assert.Empty(harness.Writer.Calls);
        Assert.False(harness.Actions.IsRunning(harness.TabId));
    }

    [Fact]
    public void GivenAnUnrelatedAction_WhenExecuting_ThenItIsNotClaimed()
    {
        using var harness = new Harness();

        Assert.False(harness.Actions.TryExecute(ActionIds.RepositoryRefresh, null));
    }

    private sealed class Harness : IDisposable
    {
        private readonly WorkspaceHost _host = new(Workspace.Empty);

        public Harness()
        {
            Root = Path.Combine(Path.GetTempPath(), "gitunia-remote-actions-tests");
            var gitDirectory = Path.Combine(Root, ".git");
            var location = new RepositoryLocation(Root, gitDirectory, gitDirectory, false, false);
            _host.Dispatch(new OpenRepositoryCommand(location));
            TabId = _host.Workspace.ActiveTabId!.Value;
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Actions = new RemoteActions(
                _host,
                Writer,
                Statuses,
                Pump.Post,
                _ => TestContext.Current.CancellationToken,
                _ =>
                {
                    Refreshes++;
                    return true;
                },
                () => { },
                Gate);
        }

        public string Root { get; }

        public TabId TabId { get; }

        public UiPump Pump { get; }

        public FakeRemoteWriter Writer { get; } = new();

        public StatusChannel Statuses { get; } = new();

        public TabOperationGate Gate { get; } = new();

        public RemoteActions Actions { get; }

        public int Refreshes { get; private set; }

        public void Execute(ActionId action) => Actions.TryExecute(action, null);

        public Task WaitAsync() => Pump.RunUntilAsync(() => !Actions.IsRunning(TabId));

        public void Dispose()
        {
        }
    }
}
