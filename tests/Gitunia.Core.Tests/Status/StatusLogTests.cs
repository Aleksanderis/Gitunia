using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Status;

public class StatusLogTests
{
    [Fact]
    public void GivenEmptyLog_WhenReadingLatest_ThenThereIsNone()
    {
        Assert.Null(StatusLog.Empty.Latest);
        Assert.Empty(StatusLog.Empty.History);
    }

    [Fact]
    public void GivenTwoPosts_WhenReadingLatest_ThenTheNewestShowsAndBothAreInHistory()
    {
        var log = StatusLog.Empty.Post("first", StatusSeverity.Info).Post("second", StatusSeverity.Error);

        Assert.Equal(new StatusMessage(2, "second", StatusSeverity.Error), log.Latest);
        Assert.Equal(["first", "second"], log.History.Select(message => message.Text));
    }

    [Fact]
    public void GivenShowingMessage_WhenDismissing_ThenLatestIsGoneButHistoryKeepsIt()
    {
        var log = StatusLog.Empty.Post("failed", StatusSeverity.Error).Dismiss();

        Assert.Null(log.Latest);
        Assert.Single(log.History);
    }

    [Fact]
    public void GivenDismissedMessage_WhenPostingAnother_ThenTheNewOneShows()
    {
        var log = StatusLog.Empty.Post("old", StatusSeverity.Info).Dismiss().Post("new", StatusSeverity.Warning);

        Assert.Equal("new", log.Latest?.Text);
        Assert.Equal(2, log.Latest?.Sequence);
    }

    [Fact]
    public void GivenNothingShowing_WhenDismissing_ThenTheLogIsUnchanged()
    {
        var log = StatusLog.Empty.Post("old", StatusSeverity.Info).Dismiss();

        Assert.Same(log, log.Dismiss());
    }

    [Fact]
    public void GivenMoreThanTheCapacity_WhenPosting_ThenTheOldestMessagesDrop()
    {
        var log = StatusLog.Empty;
        for (var index = 1; index <= StatusLog.HistoryCapacity + 5; index++)
        {
            log = log.Post("message " + index, StatusSeverity.Info);
        }

        Assert.Equal(StatusLog.HistoryCapacity, log.History.Count);
        Assert.Equal("message 6", log.History[0].Text);
        Assert.Equal(StatusLog.HistoryCapacity + 5, log.Latest?.Sequence);
    }

    [Fact]
    public void GivenDismissedMessages_WhenPostingMore_ThenSequenceKeepsGrowing()
    {
        var log = StatusLog.Empty.Post("a", StatusSeverity.Info).Post("b", StatusSeverity.Info).Dismiss().Post("c", StatusSeverity.Info);

        Assert.Equal([1L, 2L, 3L], log.History.Select(message => message.Sequence));
    }

    [Fact]
    public void GivenMultilineText_WhenPosting_ThenItIsOneLine()
    {
        var log = StatusLog.Empty.Post("fatal: bad object\r\n  hint: try again\n", StatusSeverity.Error);

        Assert.Equal("fatal: bad object hint: try again", log.Latest?.Text);
    }
}
