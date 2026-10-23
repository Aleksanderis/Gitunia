using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Status;

[Trait("Os", "Independent")]
public class StatusChannelTests
{
    [Fact]
    public void GivenChannel_WhenPostingAndDismissing_ThenChangedIsRaisedEachTime()
    {
        var channel = new StatusChannel();
        var changes = 0;
        channel.Changed += () => changes++;

        channel.Error("worktrees failed");
        var dismissed = channel.Dismiss();

        Assert.True(dismissed);
        Assert.Equal(2, changes);
        Assert.Null(channel.Log.Latest);
        Assert.Equal(StatusSeverity.Error, channel.Log.History[0].Severity);
    }

    [Fact]
    public void GivenNothingShowing_WhenDismissing_ThenItReportsFalseAndRaisesNothing()
    {
        var channel = new StatusChannel();
        var changes = 0;
        channel.Changed += () => changes++;

        Assert.False(channel.Dismiss());
        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenSameMessageStillShowing_WhenPostingItAgain_ThenHistoryHasOneEntry()
    {
        var channel = new StatusChannel();
        var changes = 0;
        channel.Changed += () => changes++;

        channel.Error("repo: could not read the stashes");
        channel.Error("repo: could not read the stashes");

        Assert.Single(channel.Log.History);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void GivenInfo_WhenItsLifetimeEnds_ThenCurrentIsGoneAndHistoryKeepsIt()
    {
        var clock = new ManualClock();
        var channel = new StatusChannel(clock);
        channel.Post("Pushed tag M3 to origin");

        clock.Advance(StatusLifetime.Of(StatusSeverity.Info) - TimeSpan.FromMilliseconds(1));
        Assert.NotNull(channel.Current);
        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Null(channel.Current);
        Assert.Single(channel.Log.History);
    }

    [Fact]
    public void GivenError_WhenInfoLifetimePasses_ThenTheErrorStillShows()
    {
        var clock = new ManualClock();
        var channel = new StatusChannel(clock);
        channel.Error("push failed");

        clock.Advance(StatusLifetime.Of(StatusSeverity.Info) + TimeSpan.FromSeconds(1));

        Assert.Equal("push failed", channel.Current?.Text);
    }

    [Fact]
    public void GivenNewMessage_WhenTimePasses_ThenItIsFreshOnlyBrieflyAndAskForARedrawAtEachStep()
    {
        var clock = new ManualClock();
        var channel = new StatusChannel(clock);
        Assert.Null(channel.UntilNextChange);

        channel.Post("done");
        Assert.True(channel.IsFresh);
        Assert.Equal(StatusLifetime.Fresh, channel.UntilNextChange);

        clock.Advance(StatusLifetime.Fresh);
        Assert.False(channel.IsFresh);
        Assert.Equal(StatusLifetime.Of(StatusSeverity.Info) - StatusLifetime.Fresh, channel.UntilNextChange);

        clock.Advance(StatusLifetime.Of(StatusSeverity.Info));
        Assert.Null(channel.UntilNextChange);
    }

    [Fact]
    public void GivenExpiredMessage_WhenPostingTheSameTextAgain_ThenItShowsAgain()
    {
        var clock = new ManualClock();
        var channel = new StatusChannel(clock);
        channel.Post("Fetched");
        clock.Advance(TimeSpan.FromMinutes(1));

        channel.Post("Fetched");

        Assert.Equal(2, channel.Log.History.Count);
        Assert.NotNull(channel.Current);
        Assert.True(channel.IsFresh);
    }

    [Fact]
    public void GivenExpiredMessage_WhenDismissing_ThenItReportsFalse()
    {
        var clock = new ManualClock();
        var channel = new StatusChannel(clock);
        channel.Post("done");
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(channel.Dismiss());
    }

    [Fact]
    public void GivenChannel_WhenTicking_ThenChangedIsRaised()
    {
        var channel = new StatusChannel();
        var changes = 0;
        channel.Changed += () => changes++;

        channel.Tick();

        Assert.Equal(1, changes);
    }

    [Fact]
    public void GivenWarning_WhenPosting_ThenSeverityIsWarning()
    {
        var channel = new StatusChannel();

        channel.Warning("session was reset");

        Assert.Equal(StatusSeverity.Warning, channel.Log.Latest?.Severity);
    }
}
