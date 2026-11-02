using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class ForceQuitKeyTests
{
    [Fact]
    public void GivenOnlyCtrlC_WhenMatched_ThenItIsTheForceQuitKeyNotPlainCOrCtrlAltCOrCtrlD()
    {
        Assert.True(ForceQuitKey.IsMatch(Key.C.WithCtrl));
        Assert.False(ForceQuitKey.IsMatch(Key.C));
        Assert.False(ForceQuitKey.IsMatch(Key.C.WithCtrl.WithAlt));
        Assert.False(ForceQuitKey.IsMatch(Key.D.WithCtrl));
    }
}
