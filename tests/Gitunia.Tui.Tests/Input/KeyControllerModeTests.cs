using Gitunia.Tui.App;
using Gitunia.App;
using Gitunia.App.Shell;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

[Trait("Os", "Independent")]
public class KeyControllerModeTests
{
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly KeyController _keys;

    public KeyControllerModeTests()
    {
        _keys = new KeyController(new KeymapEngine(DefaultKeymap.Create()), new IgnoringRoute(), _host);
    }

    [Fact]
    public void GivenAModeCommand_WhenDispatched_ThenTheControllerFollowsTheStateAndRaisesPendingChanged()
    {
        var raised = 0;
        _keys.PendingChanged += () => raised++;

        _host.SetKeyMode(KeyMode.Insert);

        Assert.Equal(KeyMode.Insert, _keys.Mode);
        Assert.Equal(KeyMode.Insert, _host.State.Shell.Mode);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void GivenAPendingChord_WhenTheModeIsSetToTheSameMode_ThenThePendingKeysAreDropped()
    {
        _keys.Handle(new Key('g'), DefaultKeymap.GraphView);
        Assert.True(_keys.State.HasPending);

        _host.SetKeyMode(KeyMode.Normal);

        Assert.False(_keys.State.HasPending);
        Assert.Equal(string.Empty, _keys.PendingText);
    }

    [Fact]
    public void GivenOtherCommands_WhenDispatched_ThenAPendingChordStays()
    {
        _keys.Handle(new Key('g'), DefaultKeymap.GraphView);

        _host.Dispatch(new StatusPostedCommand("x", StatusSeverity.Info, _host.Clock.GetUtcNow()));

        Assert.True(_keys.State.HasPending);
    }

    [Fact]
    public void GivenAConfirmDialogAsked_WhenTheShellOpensIt_ThenTheControllerIsInConfirmMode()
    {
        _host.Dispatch(new ConfirmRequestedCommand(
            new ConfirmPrompt("Delete", ["Delete x?"], "No undo.", "Delete"),
            new RetryInTerminalConfirmed(default, "Test", []),
            null));

        Assert.Equal(KeyMode.Confirm, _keys.Mode);
    }

    [Theory]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("Down")]
    [InlineData("Up")]
    [InlineData("Home")]
    [InlineData("z")]
    public void GivenTheSettingsScreen_WhenAKeyIsPressed_ThenItIsConsumedSoNoPaneBehindMoves(string name)
    {
        Assert.True(_keys.Handle(new KeyEvent(name), DefaultKeymap.SettingsView));
    }

    [Fact]
    public void GivenAPane_WhenAnUnboundKeyIsPressed_ThenItIsLeftToTheTerminal()
    {
        Assert.False(_keys.Handle(new KeyEvent("Left"), DefaultKeymap.GraphView));
    }

    private sealed class IgnoringRoute : IActionRoute
    {
        public void Route(ActionId action, int? count)
        {
        }
    }
}
