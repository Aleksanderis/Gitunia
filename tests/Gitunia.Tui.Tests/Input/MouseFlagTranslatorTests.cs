using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class MouseFlagTranslatorTests
{
    [Theory]
    [InlineData(MouseFlags.WheeledUp | MouseFlags.Shift, MouseGesture.WheelHorizontalBack)]
    [InlineData(MouseFlags.WheeledUp | MouseFlags.Ctrl, MouseGesture.WheelHorizontalBack)]
    [InlineData(MouseFlags.WheeledDown | MouseFlags.Shift, MouseGesture.WheelHorizontalForward)]
    [InlineData(MouseFlags.WheeledDown | MouseFlags.Ctrl, MouseGesture.WheelHorizontalForward)]
    public void GivenAWheelWithAModifier_WhenTranslatingSideways_ThenItIsAHorizontalNotch(
        MouseFlags flags,
        MouseGesture expected)
    {
        Assert.True(MouseFlagTranslator.TryTranslateSideways(flags, out var gesture));
        Assert.Equal(expected, gesture);
        Assert.False(MouseFlagTranslator.TryTranslate(flags, out _));
    }

    [Theory]
    [InlineData(MouseFlags.WheeledUp)]
    [InlineData(MouseFlags.WheeledDown)]
    [InlineData(MouseFlags.LeftButtonClicked)]
    [InlineData(MouseFlags.Shift)]
    public void GivenAPlainWheelOrClick_WhenTranslatingSideways_ThenItIsNotOne(MouseFlags flags)
    {
        Assert.False(MouseFlagTranslator.TryTranslateSideways(flags, out _));
    }
}
