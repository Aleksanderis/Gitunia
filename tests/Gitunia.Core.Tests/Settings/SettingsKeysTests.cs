using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class SettingsKeysTests
{
    [Fact]
    public void GivenTheSettingsView_WhenPressingAKeyInNormalMode_ThenItRunsTheDocumentedAction()
    {
        var failures = new List<string>();
        (string keys, string action)[] rows =
        [
            ("Enter", "settings.activate"),
            ("l", "settings.activate"),
            ("h", "navigate.back"),
            ("Esc", "navigate.back"),
            ("x", "settings.toggle"),
            ("/", "settings.filter"),
            ("a", "settings.add"),
            ("d", "settings.remove"),
            ("J", "settings.move-down"),
            ("K", "settings.move-up"),
            ("+", "settings.increase"),
            ("-", "settings.decrease"),
            ("j", "list.down"),
        ];

        foreach (var (keys, action) in rows)
        {
            try
            {
                CheckItRunsTheDocumentedAction(keys, action);
            }
            catch (Exception exception)
            {
                failures.Add($"{keys}, {action}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckItRunsTheDocumentedAction(string keys,
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
