using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class FilterListKeysTests
{
    [Theory]
    [InlineData("Up", "filter.previous")]
    [InlineData("Ctrl-k", "filter.previous")]
    [InlineData("Shift-Tab", "filter.previous")]
    [InlineData("Down", "filter.next")]
    [InlineData("Ctrl-j", "filter.next")]
    [InlineData("Tab", "filter.next")]
    public void GivenAFilterListInInsertMode_WhenPressingAMoveKey_ThenTheKeymapRunsTheFilterAction(string keys, string action)
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.FilterListView);
        feeder.State = feeder.State.WithMode(KeyMode.Insert);

        Assert.Equal(new ActionId(action), feeder.Feed(keys).Action);
    }

    [Theory]
    [InlineData("Up", "palette.previous")]
    [InlineData("Ctrl-k", "palette.previous")]
    [InlineData("Shift-Tab", "palette.previous")]
    [InlineData("Down", "palette.next")]
    [InlineData("Ctrl-j", "palette.next")]
    [InlineData("Tab", "palette.next")]
    public void GivenThePalette_WhenPressingAMoveKey_ThenTheSameKeysMoveItToo(string keys, string action)
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.PaletteView);
        feeder.State = feeder.State.WithMode(KeyMode.Command);

        Assert.Equal(new ActionId(action), feeder.Feed(keys).Action);
    }

    [Fact]
    public void GivenALetterInAFilterList_WhenTyping_ThenItIsTextAndNoFilterAction()
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.FilterListView);
        feeder.State = feeder.State.WithMode(KeyMode.Insert);

        Assert.Equal(KeyOutcome.TextInput, feeder.Feed("j").Outcome);
    }
}
