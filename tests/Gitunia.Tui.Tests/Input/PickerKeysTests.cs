using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class PickerKeysTests
{
    [Theory]
    [InlineData(KeyNames.Up, KeyModifiers.None, -1)]
    [InlineData(KeyNames.Down, KeyModifiers.None, 1)]
    [InlineData(KeyNames.Tab, KeyModifiers.None, 1)]
    [InlineData("k", KeyModifiers.Ctrl, -1)]
    [InlineData("j", KeyModifiers.Ctrl, 1)]
    public void GivenAMoveKey_WhenClassified_ThenTheStepIsReturned(string name, KeyModifiers modifiers, int expected)
    {
        Assert.True(PickerKeys.IsMove(new KeyEvent(name, modifiers), out var step));
        Assert.Equal(expected, step);
    }

    [Theory]
    [InlineData("j", KeyModifiers.None)]
    [InlineData("n", KeyModifiers.None)]
    [InlineData("n", KeyModifiers.Ctrl)]
    [InlineData("p", KeyModifiers.Ctrl)]
    public void GivenATextKey_WhenClassified_ThenItIsNotAMove(string name, KeyModifiers modifiers)
    {
        Assert.False(PickerKeys.IsMove(new KeyEvent(name, modifiers), out _));
    }
}
