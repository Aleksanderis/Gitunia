using Gitunia.App.Tabs.Models;
using Xunit;

namespace Gitunia.App.Tests.Tabs.Models;

[Trait("Os", "Independent")]
public class PollOptionsTests
{
    private static readonly PollOptions Options = PollOptions.Default;

    [Theory]
    [InlineData(40, 5000)] // A quick refresh stays at the configured status interval.
    [InlineData(2000, 10000)] // A slow refresh waits five times its duration.
    [InlineData(30000, 60000)] // A very slow refresh is capped at the maximum.
    public void GivenARefreshDuration_WhenIntervalIsComputed_ThenItFollowsTheDurationWithinTheLimits(
        int durationMilliseconds,
        int expectedMilliseconds)
    {
        var interval = Options.StatusIntervalAfter(TimeSpan.FromMilliseconds(durationMilliseconds));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), interval);
    }
}
