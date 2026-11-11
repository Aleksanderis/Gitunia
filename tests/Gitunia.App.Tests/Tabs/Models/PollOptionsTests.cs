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

    [Theory]
    [InlineData(1, 10000)] // A failure that was quick still doubles the minimum.
    [InlineData(2, 20000)]
    [InlineData(3, 40000)]
    [InlineData(4, 60000)] // Capped at the maximum.
    [InlineData(9, 60000)]
    public void GivenRepeatedFailures_WhenIntervalIsComputed_ThenItDoublesEachTimeUpToTheMaximum(
        int failures,
        int expectedMilliseconds)
    {
        var interval = Options.StatusIntervalAfter(TimeSpan.FromMilliseconds(10), failures);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), interval);
    }

    [Fact]
    public void GivenAFailureAfterTheTimeout_WhenIntervalIsComputed_ThenItIsTheMaximum()
    {
        var interval = Options.StatusIntervalAfter(TimeSpan.FromSeconds(30), 1);

        Assert.Equal(Options.MaximumStatusInterval, interval);
    }
}
