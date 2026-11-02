using Gitunia.Core.Blame;
using Xunit;

namespace Gitunia.Core.Tests.Blame;

public class BlameDatesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "now")]
    [InlineData(59, "now")]
    [InlineData(60, "1m ago")]
    [InlineData(59 * 60, "59m ago")]
    [InlineData(60 * 60, "1h ago")]
    [InlineData(23 * 3600, "23h ago")]
    [InlineData(24 * 3600, "1d ago")]
    [InlineData(6 * 86400, "6d ago")]
    [InlineData(7 * 86400, "1w ago")]
    [InlineData(29 * 86400, "4w ago")]
    [InlineData(30 * 86400, "1mo ago")]
    [InlineData(364 * 86400, "12mo ago")]
    [InlineData(365 * 86400, "1y ago")]
    [InlineData(800 * 86400, "2y ago")]
    public void GivenAnAge_WhenFormatting_ThenTheLargestFittingUnitIsUsed(int seconds, string expected)
    {
        Assert.Equal(expected, BlameDates.Relative(Now.AddSeconds(-seconds), Now));
    }

    [Fact]
    public void GivenATimeInTheFuture_WhenFormatting_ThenItReadsAsNow()
    {
        Assert.Equal("now", BlameDates.Relative(Now.AddDays(2), Now));
    }
}
