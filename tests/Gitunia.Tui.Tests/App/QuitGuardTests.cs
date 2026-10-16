using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class QuitGuardTests
{
    [Fact]
    public void GivenNoUnsentMessage_WhenQuitting_ThenItGoesThrough()
    {
        Assert.True(new QuitGuard().MayQuit([]));
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenQuittingTwiceInARow_ThenOnlyTheSecondGoesThrough()
    {
        var guard = new QuitGuard();

        Assert.False(guard.MayQuit(["api"]));
        Assert.True(guard.MayQuit(["api"]));
    }

    [Fact]
    public void GivenAnActionBetweenTheQuits_WhenQuittingAgain_ThenTheGuardWarnsAgain()
    {
        var guard = new QuitGuard();
        guard.MayQuit(["api"]);

        guard.Reset();

        Assert.False(guard.MayQuit(["api"]));
    }

    [Fact]
    public void GivenSeveralTabs_WhenWarning_ThenTheyAreNamed()
    {
        var warning = QuitGuard.Warning(["api", "web"]);

        Assert.Contains("api, web", warning, StringComparison.Ordinal);
        Assert.Contains("quit again", warning, StringComparison.Ordinal);
    }
}
