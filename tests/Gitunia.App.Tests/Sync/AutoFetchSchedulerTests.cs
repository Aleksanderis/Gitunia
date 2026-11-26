using Gitunia.App.Sync;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core.Sync;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Sync;

// The background fetch end to end: timer decision -> SyncRequested(AutoFetch) -> reducer -> handler -> fake writer -> result.
public sealed class AutoFetchSchedulerTests : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ManualClock _clock = new();
    private readonly FakeRemoteWriter _writer = new();
    private readonly RecordingShell _shell = new();
    private readonly TestRepository _repository = TestRepositories.WithTrackingBranch(0, 0);
    private FeaturePipeline? _pipeline;
    private AutoFetchScheduler? _scheduler;

    [Fact]
    public async Task GivenTheIntervalHasNotPassed_WhenTheTimerTicks_ThenNothingIsFetched()
    {
        await OpenAsync();

        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        _scheduler!.Tick();

        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task GivenTheIntervalHasPassed_WhenTheTimerTicks_ThenTheActiveTabFetchesWithoutAMessage()
    {
        await OpenAsync();

        _clock.Advance(Interval);
        _scheduler!.Tick();
        await _pipeline!.Pump.RunUntilAsync(() => !_pipeline.IsBusy);

        Assert.Single(_writer.Calls);
        Assert.StartsWith("fetch|", _writer.Calls[0], StringComparison.Ordinal);
        Assert.Null(_pipeline.Statuses.Log.Latest);
        Assert.Null(_pipeline.Host.Workspace.FindTab(_pipeline.TabId)!.Sync);
    }

    [Fact]
    public async Task GivenAFetchJustRan_WhenTheTimerTicksAgain_ThenItWaitsAnotherInterval()
    {
        await OpenAsync();
        _clock.Advance(Interval);
        _scheduler!.Tick();
        await _pipeline!.Pump.RunUntilAsync(() => !_pipeline.IsBusy);

        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        _scheduler.Tick();
        Assert.Single(_writer.Calls);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _scheduler.Tick();
        await _pipeline.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Equal(2, _writer.Calls.Count);
    }

    [Fact]
    public async Task GivenAutoFetchIsOff_WhenTheTimerTicks_ThenNothingIsFetched()
    {
        await OpenAsync();
        _scheduler!.Configure(false, Interval);

        _clock.Advance(TimeSpan.FromHours(2));
        _scheduler.Tick();

        Assert.Empty(_writer.Calls);
    }

    [Fact]
    public async Task GivenTheTerminalIsUnfocused_WhenTheIntervalPasses_ThenItFetchesOnFocusIn()
    {
        await OpenAsync();
        _scheduler!.SetFocused(false);

        _clock.Advance(Interval);
        _scheduler.Tick();
        Assert.Empty(_writer.Calls);

        _scheduler.SetFocused(true);
        await _pipeline!.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Single(_writer.Calls);
    }

    [Fact]
    public async Task GivenAFetchRuns_WhenTheTimerTicks_ThenTheRoundIsSkippedWithoutAMessage()
    {
        await OpenAsync();
        _writer.Blocks = true;
        _pipeline!.Dispatch(new SyncRequested(_pipeline.TabId, SyncKind.Fetch));
        await _writer.Started;

        _clock.Advance(Interval);
        _scheduler!.Tick();
        _writer.Release();
        await _pipeline.Pump.RunUntilAsync(() => !_pipeline.IsBusy);

        Assert.Single(_writer.Calls);
    }

    [Fact]
    public async Task GivenAFailedFetch_WhenTheTimerTicks_ThenItBacksOffAndStaysQuiet()
    {
        await OpenAsync();
        _writer.Failure = new GitCommandFailedException(["fetch"], 128, "fatal: unable to access: Could not resolve host");
        _clock.Advance(Interval);
        _scheduler!.Tick();
        await _pipeline!.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Null(_pipeline.Statuses.Log.Latest);
        Assert.Empty(_shell.Retries);

        _clock.Advance(Interval);
        _scheduler.Tick();
        Assert.Single(_writer.Calls);

        _clock.Advance(Interval);
        _scheduler.Tick();
        await _pipeline.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Equal(2, _writer.Calls.Count);
    }

    [Fact]
    public async Task GivenTheRemoteWantsCredentials_WhenTheTimerFetches_ThenItStopsUntilAManualFetch()
    {
        await OpenAsync();
        _writer.Failure = new GitCommandFailedException(["fetch"], 128, "Permission denied (publickey).");
        _clock.Advance(Interval);
        _scheduler!.Tick();
        await _pipeline!.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Null(_pipeline.Statuses.Log.Latest);

        _clock.Advance(TimeSpan.FromHours(3));
        _scheduler.Tick();
        Assert.Single(_writer.Calls);

        _writer.Failure = null;
        _pipeline.Dispatch(new SyncRequested(_pipeline.TabId, SyncKind.Fetch));
        await _pipeline.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        _clock.Advance(Interval);
        _scheduler.Tick();
        await _pipeline.Pump.RunUntilAsync(() => !_pipeline.IsBusy);
        Assert.Equal(3, _writer.Calls.Count);
    }

    public void Dispose()
    {
        _scheduler?.Dispose();
        _pipeline?.Dispose();
        _repository.Dispose();
    }

    private async Task OpenAsync()
    {
        var services = TestGit.Services with { Remote = _writer };
        _pipeline = await FeaturePipeline.OpenAsync(services, _repository, _shell);
        await _pipeline.Pump.RunUntilAsync(() => _pipeline.Host.Workspace.FindTab(_pipeline.TabId)!.Refs.Length > 0);
        _scheduler = new AutoFetchScheduler(_pipeline.Host, _pipeline.Pump, _clock);
        _scheduler.Configure(true, Interval);
        _pipeline.Runner.FetchEnded += _scheduler.OnFetchEnded;
    }
}
