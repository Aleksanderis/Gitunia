using Gitunia.App.Updates;
using Gitunia.Platform.Updates;
using Xunit;

namespace Gitunia.App.Tests.Updates;

[Trait("Os", "Independent")]
public class UpdateAdviceTests
{
    [Theory]
    [InlineData(InstallRoute.Homebrew, "brew upgrade gitunia")]
    [InlineData(InstallRoute.Winget, "winget upgrade Gitunia.Gitunia")]
    [InlineData(InstallRoute.Script, "gitunia update")]
    [InlineData(InstallRoute.Pacman, "gitunia update")]
    [InlineData(InstallRoute.Apt, "gitunia update")]
    [InlineData(InstallRoute.Unknown, "gitunia update")]
    public void GivenAnInstallRoute_WhenAskingForTheHintCommand_ThenOnlyHomebrewAndWingetNameTheirOwn(
        InstallRoute route,
        string expected)
    {
        Assert.Equal(expected, UpdateAdvice.HintCommandFor(route));
    }
}
