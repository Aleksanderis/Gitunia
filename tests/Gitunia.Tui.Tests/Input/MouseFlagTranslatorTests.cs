using Gitunia.Core.Mouse;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class MouseFlagTranslatorTests
{
    [Theory]
    [InlineData(MouseFlags.LeftButtonClicked, MouseGesture.Click)]
    [InlineData(MouseFlags.LeftButtonDoubleClicked, MouseGesture.DoubleClick)]
    [InlineData(MouseFlags.RightButtonClicked, MouseGesture.RightClick)]
    [InlineData(MouseFlags.WheeledUp, MouseGesture.WheelUp)]
    [InlineData(MouseFlags.WheeledDown, MouseGesture.WheelDown)]
    public void GivenAMappedFlag_WhenTranslating_ThenTheGestureIsReturned(MouseFlags flags, MouseGesture expected)
    {
        Assert.True(MouseFlagTranslator.TryTranslate(flags, out var gesture));
        Assert.Equal(expected, gesture);
    }

    [Theory]
    [InlineData(MouseFlags.LeftButtonPressed)]
    [InlineData(MouseFlags.LeftButtonReleased)]
    [InlineData(MouseFlags.WheeledLeft)]
    [InlineData(MouseFlags.LeftButtonTripleClicked)]
    [InlineData(MouseFlags.None)]
    public void GivenAFlagTheMapIgnores_WhenTranslating_ThenNothingIsReturned(MouseFlags flags)
    {
        Assert.False(MouseFlagTranslator.TryTranslate(flags, out _));
    }

    [Theory]
    [InlineData(MouseFlags.Ctrl)]
    [InlineData(MouseFlags.Shift)]
    public void GivenAWheelWithAModifier_WhenTranslating_ThenItIsTreatedAsSidewaysAndIgnored(MouseFlags modifier)
    {
        Assert.False(MouseFlagTranslator.TryTranslate(MouseFlags.WheeledDown | modifier, out _));
    }

    [Theory]
    [InlineData(MouseFlags.LeftButtonClicked, true)]
    [InlineData(MouseFlags.LeftButtonDoubleClicked, true)]
    [InlineData(MouseFlags.LeftButtonTripleClicked, true)]
    [InlineData(MouseFlags.LeftButtonPressed, false)]
    [InlineData(MouseFlags.WheeledUp, false)]
    public void GivenAFlag_WhenAskingIfItIsALeftClick_ThenEveryClickOfABurstCounts(MouseFlags flags, bool expected)
    {
        Assert.Equal(expected, MouseFlagTranslator.IsLeftClick(flags));
    }
}
