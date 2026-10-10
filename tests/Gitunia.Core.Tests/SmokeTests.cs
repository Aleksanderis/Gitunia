using Gitunia.Core;
using Xunit;

namespace Gitunia.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void GivenAppInfo_WhenReadingBanner_ThenItStartsWithTheAppName() => Assert.StartsWith("gitunia", AppInfo.Banner);
}
