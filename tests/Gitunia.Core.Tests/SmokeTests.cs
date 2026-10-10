using Gitunia.Core;
using Xunit;

namespace Gitunia.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Given_app_info_When_reading_banner_Then_it_starts_with_the_app_name() => Assert.StartsWith("gitunia", AppInfo.Banner);
}
