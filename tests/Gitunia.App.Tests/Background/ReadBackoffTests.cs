using Gitunia.App.Background;
using Gitunia.App.Tests.Fakes;
using Xunit;

namespace Gitunia.App.Tests.Background;

public class ReadBackoffTests
{
    private const string Repository = "/repository";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    [Fact]
    public void GivenUnknownRepository_WhenAsked_ThenItIsDue()
    {
        var backoff = new ReadBackoff(Interval, new ManualClock());

        Assert.True(backoff.IsDue(Repository));
    }

    [Fact]
    public void GivenFastSuccess_WhenRecorded_ThenTheRepositoryStaysDue()
    {
        var backoff = new ReadBackoff(Interval, new ManualClock());

        backoff.RecordSuccess(Repository, TimeSpan.FromMilliseconds(50));

        Assert.True(backoff.IsDue(Repository));
    }

    [Fact]
    public void GivenSlowSuccess_WhenRecorded_ThenTheRepositoryWaitsLongerThanAPass()
    {
        var clock = new ManualClock();
        var backoff = new ReadBackoff(Interval, clock);

        backoff.RecordSuccess(Repository, TimeSpan.FromSeconds(20));
        clock.Advance(Interval);

        Assert.False(backoff.IsDue(Repository));
        clock.Advance(TimeSpan.FromSeconds(40));
        Assert.True(backoff.IsDue(Repository));
    }

    [Fact]
    public void GivenRepeatedFailures_WhenRecorded_ThenEachSkipIsLongerThanTheLast()
    {
        var clock = new ManualClock();
        var backoff = new ReadBackoff(Interval, clock);
        var skips = new List<TimeSpan>();

        for (var failure = 0; failure < 4; failure++)
        {
            backoff.RecordFailure(Repository, TimeSpan.FromSeconds(1));
            skips.Add(SkipLength(backoff, clock));
        }

        Assert.True(skips[0] >= Interval);
        Assert.True(skips[1] > skips[0]);
        Assert.True(skips[2] > skips[1]);
        Assert.Equal(4, backoff.Failures(Repository));
    }

    [Fact]
    public void GivenFailures_WhenAReadSucceeds_ThenTheRepositoryIsDueAgain()
    {
        var backoff = new ReadBackoff(Interval, new ManualClock());
        backoff.RecordFailure(Repository, TimeSpan.FromSeconds(1));

        backoff.RecordSuccess(Repository, TimeSpan.FromMilliseconds(50));

        Assert.True(backoff.IsDue(Repository));
        Assert.Equal(0, backoff.Failures(Repository));
    }

    [Fact]
    public void GivenManyFailures_WhenRecorded_ThenTheSkipIsCappedAtTheMaximum()
    {
        var clock = new ManualClock();
        var maximum = TimeSpan.FromMinutes(5);
        var backoff = new ReadBackoff(Interval, clock, maximum);

        for (var failure = 0; failure < 20; failure++)
        {
            backoff.RecordFailure(Repository, TimeSpan.FromSeconds(30));
        }

        clock.Advance(maximum);
        Assert.True(backoff.IsDue(Repository));
    }

    private static TimeSpan SkipLength(ReadBackoff backoff, ManualClock clock)
    {
        var step = TimeSpan.FromSeconds(5);
        var waited = TimeSpan.Zero;
        while (!backoff.IsDue(Repository))
        {
            clock.Advance(step);
            waited += step;
        }

        return waited;
    }
}
