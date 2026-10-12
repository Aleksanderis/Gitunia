using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Status;

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
    public void GivenWarning_WhenPosting_ThenSeverityIsWarning()
    {
        var channel = new StatusChannel();

        channel.Warning("session was reset");

        Assert.Equal(StatusSeverity.Warning, channel.Log.Latest?.Severity);
    }
}
