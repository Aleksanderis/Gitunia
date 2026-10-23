using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

[Trait("Os", "Independent")]
public class QuitGuardTests
{
    [Fact]
    public void GivenUnsentMessageAndFailedSave_WhenQuittingTwice_ThenTheFirstIsHeldBack()
    {
        var guard = new QuitGuard(() => true, () => false);

        Assert.False(guard.ShouldQuit());
        Assert.True(guard.ShouldQuit());
    }

    [Fact]
    public void GivenWarning_WhenTheDraftChangesAndSaveStillFails_ThenItWarnsAgain()
    {
        var guard = new QuitGuard(() => true, () => false);
        Assert.False(guard.ShouldQuit());

        guard.Reset();

        Assert.False(guard.ShouldQuit());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void GivenNoUnsentMessageOrASuccessfulSave_WhenQuitting_ThenItLeavesAtOnce(bool unsent, bool saved)
    {
        var guard = new QuitGuard(() => unsent, () => saved);

        Assert.True(guard.ShouldQuit());
    }
}
