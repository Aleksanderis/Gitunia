using Xunit;

namespace Gitunia.Core.Tests;

[Trait("Os", "Independent")]
public class SmokeTests
{
    [Fact]
    public void GivenAppInfo_WhenReadingBanner_ThenItStartsWithTheAppName() => Assert.StartsWith("gitunia", AppInfo.Banner);
}
