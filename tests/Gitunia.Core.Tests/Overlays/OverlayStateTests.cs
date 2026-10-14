using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class OverlayStateTests
{
    [Fact]
    public void GivenNoOverlay_WhenOpeningHelp_ThenItRemembersTheViewAndModeToReturnTo()
    {
        var state = OverlayState.OpenHelp(DefaultKeymap.GraphView, KeyMode.Visual);

        Assert.Equal(OverlayKind.Help, state.Kind);
        Assert.Equal(DefaultKeymap.GraphView, state.HelpView);
        Assert.Equal(KeyMode.Visual, state.ReturnMode);
        Assert.True(state.IsOpen);
    }

    [Fact]
    public void GivenOpenHelp_WhenScrollingAboveTheTop_ThenItStaysAtTheTop()
    {
        Assert.Equal(0, OverlayState.OpenHelp("Graph", KeyMode.Normal).ScrollHelpTo(-4).HelpScroll);
    }

    [Fact]
    public void GivenAnOpenedOverlay_WhenWhichKeyWasShowing_ThenThePopupStartsHidden()
    {
        var state = OverlayState.OpenPalette(new PaletteState([]), KeyMode.Normal);

        Assert.False(state.WhichKeyVisible);
        Assert.True(state.WithWhichKey(true).WhichKeyVisible);
    }
}
