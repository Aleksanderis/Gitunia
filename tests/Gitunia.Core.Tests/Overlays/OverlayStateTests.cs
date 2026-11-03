using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

[Trait("Os", "Independent")]
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
    public void GivenAHelpFilter_WhenEditingWithTheCaretInTheMiddle_ThenKeysEditAtTheCaret()
    {
        var state = OverlayState.OpenHelp("Graph", KeyMode.Normal).OpenHelpFilter().ScrollHelpTo(5);
        foreach (var name in new[] { "a", "c" })
        {
            state = state.EditHelpFilter(new KeyEvent(name));
        }

        state = state.EditHelpFilter(new KeyEvent(KeyNames.Left)).EditHelpFilter(new KeyEvent("b"));
        Assert.Equal(("abc", 2), (state.HelpFilter, state.HelpFilterCaret));

        state = state.EditHelpFilter(new KeyEvent(KeyNames.Backspace));
        Assert.Equal(("ac", 1), (state.HelpFilter, state.HelpFilterCaret));
    }

    [Fact]
    public void GivenAHelpFilter_WhenOnlyTheCaretMoves_ThenTheScrollStays()
    {
        var state = OverlayState.OpenHelp("Graph", KeyMode.Normal).OpenHelpFilter().EditHelpFilter(new KeyEvent("a"));
        state = state.ScrollHelpTo(3).EditHelpFilter(new KeyEvent(KeyNames.Home));

        Assert.Equal((3, 0), (state.HelpScroll, state.HelpFilterCaret));
        Assert.Same(state, state.EditHelpFilter(new KeyEvent(KeyNames.Home)));
    }

    [Fact]
    public void GivenAnOpenedOverlay_WhenWhichKeyWasShowing_ThenThePopupStartsHidden()
    {
        var state = OverlayState.OpenPalette(new PaletteState([]), KeyMode.Normal);

        Assert.False(state.WhichKeyVisible);
        Assert.True(state.WithWhichKey(true).WhichKeyVisible);
    }
}
