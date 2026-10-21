using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class KeyControllerPasteTests
{
    private readonly List<ExecutedAction> _executed = [];
    private readonly List<string> _typed = [];

    [Theory]
    [InlineData(KeyMode.Insert)]
    [InlineData(KeyMode.Command)]
    public void GivenATextMode_WhenPasting_ThenEveryCharacterReachesTheTextHandler(KeyMode mode)
    {
        var controller = CreateController(mode);

        var taken = controller.Paste("https://x/a.git", multiLine: false, () => DefaultKeymap.PickerView);

        Assert.True(taken);
        Assert.Equal("https://x/a.git", string.Concat(_typed));
    }

    [Theory]
    [InlineData(KeyMode.Normal)]
    [InlineData(KeyMode.Visual)]
    public void GivenANonTextMode_WhenPasting_ThenNothingRunsAndThePasteIsNotTaken(KeyMode mode)
    {
        var controller = CreateController(mode);

        var taken = controller.Paste("quit", multiLine: false, () => DefaultKeymap.GraphView);

        Assert.False(taken);
        Assert.Empty(_typed);
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenASingleLinePaste_WhenItEndsWithALineBreak_ThenNoAcceptActionRuns()
    {
        var controller = CreateController(KeyMode.Insert);

        controller.Paste("url\r\n", multiLine: false, () => DefaultKeymap.PickerView);

        Assert.Equal("url", string.Concat(_typed));
        Assert.Empty(_executed);
    }

    [Fact]
    public void GivenAKeyThatLeavesTheTextMode_WhenPasting_ThenTheRestIsNotRunAsNormalKeys()
    {
        var controller = CreateController(KeyMode.Insert);
        controller.TextKeyHandler = key =>
        {
            _typed.Add(key.Name);
            controller.SetMode(KeyMode.Normal);
            return true;
        };

        controller.Paste("abcd", multiLine: false, () => DefaultKeymap.GraphView);

        Assert.Equal(["a"], _typed);
        Assert.Empty(_executed);
    }

    private KeyController CreateController(KeyMode mode)
    {
        var controller = new KeyController(
            new KeymapEngine(DefaultKeymap.Create()),
            (action, count) => _executed.Add(new ExecutedAction(action, count)))
        {
            TextKeyHandler = key =>
            {
                _typed.Add(key.Name);
                return true;
            },
        };
        controller.SetMode(mode);

        return controller;
    }
}
