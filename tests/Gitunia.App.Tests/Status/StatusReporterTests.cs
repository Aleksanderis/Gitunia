using System.Runtime.CompilerServices;
using Gitunia.App.Status;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.App.Tests.Status;

[Trait("Os", "Independent")]
public class StatusReporterTests
{
    [Fact]
    public void GivenAReporter_WhenPostingAndDismissing_ThenTheStateChangesAndTheHostRaisesChangedEachTime()
    {
        var host = new Dispatcher(AppState.Empty);
        var reporter = new StatusReporter(host);
        var changes = 0;
        host.Changed += () => changes++;

        reporter.Error("worktrees failed");
        var dismissed = reporter.Dismiss();

        Assert.True(dismissed);
        Assert.Equal(2, changes);
        Assert.Null(reporter.Log.Latest);
        Assert.Equal(StatusSeverity.Error, host.State.Shell.Status.History[0].Severity);
    }

    [Fact]
    public void GivenNothingShowing_WhenDismissing_ThenItReportsFalseAndDispatchesNothing()
    {
        var host = new Dispatcher(AppState.Empty);
        var reporter = new StatusReporter(host);
        var changes = 0;
        host.Changed += () => changes++;

        Assert.False(reporter.Dismiss());
        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenInfo_WhenItsLifetimeEnds_ThenCurrentIsGoneAndHistoryKeepsIt()
    {
        var clock = new ManualClock();
        var reporter = new StatusReporter(new Dispatcher(AppState.Empty, clock: clock));
        reporter.Post("Pushed tag M3 to origin");

        clock.Advance(StatusLifetime.Of(StatusSeverity.Info) - TimeSpan.FromMilliseconds(1));
        Assert.NotNull(reporter.Current);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Null(reporter.Current);
        Assert.Single(reporter.Log.History);
    }

    [Fact]
    public void GivenNewMessage_WhenTimePasses_ThenItIsFreshOnlyBriefly()
    {
        var clock = new ManualClock();
        var reporter = new StatusReporter(new Dispatcher(AppState.Empty, clock: clock));
        reporter.Post("done");
        Assert.True(reporter.IsFresh);

        clock.Advance(StatusLifetime.Fresh);

        Assert.False(reporter.IsFresh);
        Assert.NotNull(reporter.Current);
    }

    [Fact]
    public async Task GivenAScheduledAging_WhenTheDelayPasses_ThenTheHostReducesAnAgedCommandAndRaisesChanged()
    {
        var clock = new ManualClock();
        var host = new Dispatcher(AppState.Empty, clock: clock);
        using var timer = new StatusAgingTimer(host);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Changed += () => changed.TrySetResult();
        timer.Schedule(TimeSpan.FromMilliseconds(10));
        Assert.False(changed.Task.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));

        await changed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GivenTwoSchedules_WhenTheSecondReplacesTheFirst_ThenOnlyOneAgedCommandArrives()
    {
        var clock = new ManualClock();
        var host = new Dispatcher(AppState.Empty, clock: clock);
        using var timer = new StatusAgingTimer(host);
        var changes = new StrongBox<int>();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Changed += () =>
        {
            Interlocked.Increment(ref changes.Value);
            changed.TrySetResult();
        };
        timer.Schedule(TimeSpan.FromMilliseconds(50));
        timer.Schedule(TimeSpan.FromMilliseconds(10));

        clock.Advance(TimeSpan.FromSeconds(1));
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(1, Volatile.Read(ref changes.Value));
    }
}
