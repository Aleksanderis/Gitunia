using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsKeysTests
{
    [Theory]
    [InlineData("Enter", "settings.activate")]
    [InlineData("l", "settings.activate")]
    [InlineData("h", "navigate.back")]
    [InlineData("Esc", "navigate.back")]
    [InlineData("x", "settings.toggle")]
    [InlineData("/", "settings.filter")]
    [InlineData("a", "settings.add")]
    [InlineData("d", "settings.remove")]
    [InlineData("J", "settings.move-down")]
    [InlineData("K", "settings.move-up")]
    [InlineData("+", "settings.increase")]
    [InlineData("-", "settings.decrease")]
    [InlineData("j", "list.down")]
    public void GivenTheSettingsView_WhenPressingAKeyInNormalMode_ThenItRunsTheDocumentedAction(
        string keys,
        string action)
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.SettingsView);

        Assert.Equal(action, feeder.Feed(keys).Action?.Value);
    }

    [Fact]
    public void GivenTheGraph_WhenPressingTheLeaderComma_ThenTheSettingsOpen()
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()));

        Assert.Equal(ActionIds.SettingsOpen, feeder.Feed("<space>,").Action);
    }

    [Theory]
    [InlineData(KeyMode.Insert, "Enter", "input.accept")]
    [InlineData(KeyMode.Insert, "Esc", "cancel")]
    [InlineData(KeyMode.Confirm, "y", "confirm.yes")]
    [InlineData(KeyMode.Confirm, "n", "confirm.no")]
    [InlineData(KeyMode.Confirm, "Esc", "cancel")]
    public void GivenTypingOrTheRemovalQuestion_WhenPressingItsKeys_ThenTheSettingsViewGetsTheModalActions(
        KeyMode mode,
        string keys,
        string action)
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.SettingsView)
        {
            State = KeymapState.Initial.WithMode(mode),
        };

        Assert.Equal(action, feeder.Feed(keys).Action?.Value);
    }

    [Fact]
    public void GivenConfirmMode_WhenPressingEnterInTheSettingsView_ThenNothingIsConfirmed()
    {
        var feeder = new KeyFeeder(new KeymapEngine(DefaultKeymap.Create()), DefaultKeymap.SettingsView)
        {
            State = KeymapState.Initial.WithMode(KeyMode.Confirm),
        };

        Assert.Null(feeder.Feed("Enter").Action);
    }
}
