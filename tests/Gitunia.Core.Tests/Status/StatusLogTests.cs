using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Status;

public class StatusLogTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenEmptyLog_WhenReadingLatest_ThenThereIsNone()
    {
        Assert.Null(StatusLog.Empty.Latest);
        Assert.Empty(StatusLog.Empty.History);
    }

    [Fact]
    public void GivenTwoPosts_WhenReadingLatest_ThenTheNewestShowsAndBothAreInHistory()
    {
        var log = StatusLog.Empty.Post("first", StatusSeverity.Info, Start).Post("second", StatusSeverity.Error, Start);

        Assert.Equal(new StatusMessage(2, "second", StatusSeverity.Error, Start), log.Latest);
        Assert.Equal(["first", "second"], log.History.Select(message => message.Text));
    }

    [Fact]
    public void GivenShowingMessage_WhenDismissing_ThenLatestIsGoneButHistoryKeepsIt()
    {
        var log = StatusLog.Empty.Post("failed", StatusSeverity.Error, Start).Dismiss();

        Assert.Null(log.Latest);
        Assert.Single(log.History);
    }

    [Fact]
    public void GivenDismissedMessage_WhenPostingAnother_ThenTheNewOneShows()
    {
        var log = StatusLog.Empty
            .Post("old", StatusSeverity.Info, Start)
            .Dismiss()
            .Post("new", StatusSeverity.Warning, Start);

        Assert.Equal("new", log.Latest?.Text);
        Assert.Equal(2, log.Latest?.Sequence);
    }

    [Fact]
    public void GivenNothingShowing_WhenDismissing_ThenTheLogIsUnchanged()
    {
        var log = StatusLog.Empty.Post("old", StatusSeverity.Info, Start).Dismiss();

        Assert.Same(log, log.Dismiss());
    }

    [Fact]
    public void GivenMoreThanTheCapacity_WhenPosting_ThenTheOldestMessagesDrop()
    {
        var log = StatusLog.Empty;
        for (var index = 1; index <= StatusLog.HistoryCapacity + 5; index++)
        {
            log = log.Post($"message {index}", StatusSeverity.Info, Start);
        }

        Assert.Equal(StatusLog.HistoryCapacity, log.History.Count);
        Assert.Equal("message 6", log.History[0].Text);
        Assert.Equal(StatusLog.HistoryCapacity + 5, log.Latest?.Sequence);
    }

    [Fact]
    public void GivenDismissedMessages_WhenPostingMore_ThenSequenceKeepsGrowing()
    {
        var log = StatusLog.Empty
            .Post("a", StatusSeverity.Info, Start)
            .Post("b", StatusSeverity.Info, Start)
            .Dismiss()
            .Post("c", StatusSeverity.Info, Start);

        Assert.Equal([1L, 2L, 3L], log.History.Select(message => message.Sequence));
    }

    [Fact]
    public void GivenMultilineText_WhenPosting_ThenItIsOneLine()
    {
        var log = StatusLog.Empty.Post("fatal: bad object\r\n  hint: try again\n", StatusSeverity.Error, Start);

        Assert.Equal("fatal: bad object hint: try again", log.Latest?.Text);
    }

    [Theory]
    [InlineData(StatusSeverity.Info, 4999, true)]
    [InlineData(StatusSeverity.Info, 5000, false)]
    [InlineData(StatusSeverity.Warning, 14999, true)]
    [InlineData(StatusSeverity.Warning, 15000, false)]
    [InlineData(StatusSeverity.Error, 29999, true)]
    [InlineData(StatusSeverity.Error, 30000, false)]
    public void GivenMessage_WhenTimePasses_ThenItShowsUntilItsSeverityLifetimeEnds(
        StatusSeverity severity,
        int elapsedMilliseconds,
        bool shows)
    {
        var log = StatusLog.Empty.Post("done", severity, Start);

        var current = log.Current(Start.AddMilliseconds(elapsedMilliseconds));

        Assert.Equal(shows, current is not null);
    }

    [Fact]
    public void GivenExpiredMessage_WhenReadingHistory_ThenItIsStillThere()
    {
        var log = StatusLog.Empty.Post("done", StatusSeverity.Info, Start);

        Assert.Null(log.Current(Start.AddMinutes(10)));
        Assert.Single(log.History);
    }

    [Fact]
    public void GivenSeverities_WhenComparingLifetimes_ThenProblemsOutliveSuccesses()
    {
        Assert.True(StatusLifetime.Of(StatusSeverity.Info) < StatusLifetime.Of(StatusSeverity.Warning));
        Assert.True(StatusLifetime.Of(StatusSeverity.Warning) < StatusLifetime.Of(StatusSeverity.Error));
    }

    [Fact]
    public void GivenFreshMessage_WhenAskingForTheNextChange_ThenFreshEndsBeforeItExpires()
    {
        var message = StatusLog.Empty.Post("done", StatusSeverity.Info, Start).Latest!;

        Assert.Equal(Start + StatusLifetime.Fresh, StatusLifetime.NextChange(message, Start));
        Assert.Equal(
            Start + StatusLifetime.Of(StatusSeverity.Info),
            StatusLifetime.NextChange(message, Start + StatusLifetime.Fresh));
        Assert.Null(StatusLifetime.NextChange(message, Start.AddMinutes(1)));
    }
}
