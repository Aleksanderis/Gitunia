using Gitunia.Core;
using Xunit;

namespace Gitunia.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Banner_names_the_app() => Assert.StartsWith("gitunia", AppInfo.Banner);
}
