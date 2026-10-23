using Gitunia.Tui.App.Tabs.Models;
using Xunit;

namespace Gitunia.Tui.Tests.App.Tabs.Models;

[Trait("Os", "Independent")]
public class PollOptionsTests
{
    private static readonly PollOptions Options = PollOptions.Default;

    [Fact]
    public void GivenQuickRefresh_WhenIntervalIsComputed_ThenItStaysAtTheConfiguredStatusInterval()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Options.StatusIntervalAfter(TimeSpan.FromMilliseconds(40)));
    }

    [Fact]
    public void GivenSlowRefresh_WhenIntervalIsComputed_ThenItIsFiveTimesTheDuration()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), Options.StatusIntervalAfter(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void GivenVerySlowRefresh_WhenIntervalIsComputed_ThenItIsCappedAtTheMaximum()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), Options.StatusIntervalAfter(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void GivenDefaults_WhenRead_ThenRefsArePolledEveryTwoSecondsAndStatusEveryFive()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), Options.RefsInterval);
        Assert.Equal(TimeSpan.FromSeconds(5), Options.StatusInterval);
    }
}
