using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Sync;

[Trait("Os", "Independent")]
public class AutoFetchScheduleTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TabId Tab = new(1);

    [Fact]
    public void GivenAFreshSchedule_WhenAskedAround_ThenItIsDueAfterOneInterval()
    {
        var schedule = AutoFetchSchedule.StartingAt(Start);

        Assert.False(schedule.IsDue(Start + Interval - TimeSpan.FromSeconds(1), Interval));
        Assert.True(schedule.IsDue(Start + Interval, Interval));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 30)]
    [InlineData(9, 30)]
    public void GivenFailuresInARow_WhenReadingTheDelay_ThenItDoublesUpToThirtyMinutes(int failures, int minutes)
    {
        var schedule = new AutoFetchSchedule(Start, failures, false);

        Assert.Equal(TimeSpan.FromMinutes(minutes), schedule.Delay(Interval));
    }

    [Fact]
    public void GivenAnIntervalOverTheCap_WhenFailing_ThenTheDelayStaysTheInterval()
    {
        var schedule = new AutoFetchSchedule(Start, 4, false);

        Assert.Equal(TimeSpan.FromMinutes(60), schedule.Delay(TimeSpan.FromMinutes(60)));
    }

    [Fact]
    public void GivenABackgroundFailure_WhenItEnds_ThenTheWaitDoublesFromNow()
    {
        var failed = AutoFetchSchedule.StartingAt(Start)
            .Ended(new FetchEnded(Tab, FetchOutcome.Failed, true), Start + Interval);

        Assert.Equal(1, failed.Failures);
        Assert.False(failed.IsDue(Start + Interval + Interval, Interval));
        Assert.True(failed.IsDue(Start + Interval + (Interval * 2), Interval));
    }

    [Fact]
    public void GivenABackgroundFetchNeedsCredentials_WhenItEnds_ThenTheScheduleIsNeverDue()
    {
        var stopped = AutoFetchSchedule.StartingAt(Start)
            .Ended(new FetchEnded(Tab, FetchOutcome.NeedsCredentials, true), Start);

        Assert.True(stopped.IsStopped);
        Assert.False(stopped.IsDue(Start + TimeSpan.FromDays(1), Interval));
    }

    [Fact]
    public void GivenAStoppedSchedule_WhenAManualFetchEnds_ThenItRunsAgainAfterTheInterval()
    {
        var stopped = new AutoFetchSchedule(Start, 3, true);

        var failedByHand = stopped.Ended(new FetchEnded(Tab, FetchOutcome.Failed, false), Start);
        var succeeded = stopped.Ended(new FetchEnded(Tab, FetchOutcome.Succeeded, false), Start);

        Assert.False(failedByHand.IsStopped);
        Assert.Equal(3, failedByHand.Failures);
        Assert.Equal(new AutoFetchSchedule(Start, 0, false), succeeded);
    }

    [Fact]
    public void GivenAStartedFetch_WhenAskedAgain_ThenItWaitsFromTheStart()
    {
        var started = AutoFetchSchedule.StartingAt(Start).Started(Start + Interval);

        Assert.False(started.IsDue(Start + Interval + TimeSpan.FromSeconds(1), Interval));
    }
}
