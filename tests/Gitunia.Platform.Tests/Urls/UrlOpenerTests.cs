using Gitunia.Platform.Urls;
using Xunit;

namespace Gitunia.Platform.Tests.Urls;

public class UrlOpenerTests
{
    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("calc.exe")]
    public void GivenAnAddressThatIsNotHttp_WhenOpening_ThenNothingIsStarted(string url)
    {
        var result = new UrlOpener().Open(url);

        Assert.False(result.Opened);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void GivenTheCurrentOperatingSystem_WhenAsked_ThenSupportMatchesTheBranchesOfTheOpener()
    {
        var expected = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

        Assert.Equal(expected, new UrlOpener().IsSupported);
    }
}
