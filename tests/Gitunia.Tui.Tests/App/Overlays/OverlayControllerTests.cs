using System.Collections.Concurrent;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Status;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

public sealed class OverlayControllerTests : IDisposable
{
    private readonly List<ActionId> _routed = [];
    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly SemaphoreSlim _postedSignal = new(0);
    private readonly KeyController _keys;
    private readonly OverlayController _overlays;
    private readonly WhichKeyTimer _timer;
    private readonly StatusChannel _statuses = new();
    private int _changes;

    public OverlayControllerTests()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        OverlayController? controller = null;
        _keys = new KeyController(engine, (action, count) => RouteAction(controller!, action, count));
        _timer = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        });
        controller = new OverlayController(engine, _keys, _timer, (action, count) => RouteAction(controller!, action, count), () => new ScreenSize(80, 24), GraphGlyphSet.Unicode, () => DefaultKeymap.GraphView, _statuses);
        _overlays = controller;
        _overlays.Changed += () => _changes++;
    }

    public void Dispose()
    {
        _timer.Dispose();
        _postedSignal.Dispose();
    }

    private void RunPosted()
    {
        while (_posted.TryDequeue(out var action))
        {
            action();
        }
    }

    private void RouteAction(OverlayController controller, ActionId action, int? count)
    {
        if (!controller.Handle(action, count))
        {
            _routed.Add(action);
        }
    }

    private bool Press(Key key) => _keys.Handle(key, _overlays.ViewName);

    [Fact]
    public void GivenGraphView_WhenPressingQuestionMark_ThenHelpOpensForTheGraphViewAndNormalModeStays()
    {
        Press(new Key('?'));

        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);
        Assert.Equal(DefaultKeymap.GraphView, _overlays.State.HelpView);
        Assert.Equal(DefaultKeymap.HelpView, _overlays.ViewName);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.NotNull(_overlays.CurrentFrame());
    }

    [Fact]
    public void GivenAnOpenPalette_WhenAClickLandsOnAnEntryLine_ThenThatEntryIsHighlighted()
    {
        Press(new Key(':'));
        var changesBefore = _changes;

        var selected = _overlays.SelectPaletteEntryAt(4);

        Assert.True(selected);
        Assert.Equal(3, _overlays.State.Palette!.SelectedIndex);
        Assert.True(_changes > changesBefore);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenAClickLandsOnTheQueryBar_ThenNothingIsHighlighted()
    {
        Press(new Key(':'));

        Assert.False(_overlays.SelectPaletteEntryAt(0));
        Assert.Equal(0, _overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenNoPalette_WhenAClickArrives_ThenNothingIsHighlighted()
    {
        Press(new Key('?'));

        Assert.False(_overlays.SelectPaletteEntryAt(2));
    }

    [Fact]
    public void GivenAHelpOverlayScrolledWithTheWheelAction_WhenAskedToMoveDown_ThenItScrolls()
    {
        Press(new Key('?'));

        _overlays.Handle(ActionIds.ListDown, 3);

        Assert.Equal(3, _overlays.State.HelpScroll);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenTheWheelActionsArrive_ThenTheHighlightMoves()
    {
        Press(new Key(':'));

        _overlays.Handle(ActionIds.PaletteNext, null);
        _overlays.Handle(ActionIds.PaletteNext, null);
        _overlays.Handle(ActionIds.PalettePrevious, null);

        Assert.Equal(1, _overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenOpenHelp_WhenPressingEscQuestionMarkOrQ_ThenItCloses()
    {
        foreach (var close in new[] { Key.Esc, new Key('?'), new Key('q') })
        {
            Press(new Key('?'));
            Press(close);

            Assert.False(_overlays.State.IsOpen);
            Assert.Null(_overlays.CurrentFrame());
        }

        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenOpenHelp_WhenPressingOtherActionKeys_ThenTheyAreSwallowedAndJScrolls()
    {
        Press(new Key('?'));

        Press(new Key('j'));
        Press(Key.Tab);

        Assert.Equal(1, _overlays.State.HelpScroll);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenMessages_WhenPressingSpaceM_ThenTheHistoryOpensNewestFirstAndEscCloses()
    {
        _statuses.Error("older failure");
        _statuses.Warning("newer warning");

        Press(new Key(' '));
        Press(new Key('m'));
        var opened = _overlays.State.Kind;
        var lines = _overlays.CurrentFrame()!.Lines.Select(line => line.Text).ToList();
        Press(Key.Esc);

        Assert.Equal(OverlayKind.Messages, opened);
        Assert.Contains("Status messages", lines[0], StringComparison.Ordinal);
        Assert.Contains("newer warning", lines[2], StringComparison.Ordinal);
        Assert.Contains("older failure", lines[3], StringComparison.Ordinal);
        Assert.False(_overlays.State.IsOpen);
    }

    [Fact]
    public void GivenOpenHistory_WhenAMessageIsPosted_ThenItShowsWithoutReopening()
    {
        _statuses.Error("first failure");
        Press(new Key(' '));
        Press(new Key('m'));
        var changes = 0;
        _overlays.Changed += () => changes++;

        _statuses.Warning("late warning");
        var lines = _overlays.CurrentFrame()!.Lines.Select(line => line.Text).ToList();

        Assert.Equal(OverlayKind.Messages, _overlays.State.Kind);
        Assert.Equal(1, changes);
        Assert.Contains("late warning", lines[2], StringComparison.Ordinal);
        Assert.Contains("first failure", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoMessages_WhenOpeningTheHistory_ThenItSaysSo()
    {
        Press(new Key(' '));
        Press(new Key('m'));

        Assert.Contains("no messages yet", string.Join('\n', _overlays.CurrentFrame()!.Lines.Select(line => line.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGraphView_WhenPressingColon_ThenThePaletteOpensInCommandMode()
    {
        Press(new Key(':'));

        Assert.Equal(OverlayKind.Palette, _overlays.State.Kind);
        Assert.Equal(KeyMode.Command, _keys.Mode);
        Assert.Equal(DefaultKeymap.PaletteView, _overlays.ViewName);
    }

    [Fact]
    public void GivenInsertMode_WhenPressingCtrlP_ThenThePaletteOpensAndEscRestoresInsertMode()
    {
        _keys.SetMode(KeyMode.Insert);

        Press(Key.P.WithCtrl);
        var opened = _overlays.State.Kind;
        Press(Key.Esc);

        Assert.Equal(OverlayKind.Palette, opened);
        Assert.False(_overlays.State.IsOpen);
        Assert.Equal(KeyMode.Insert, _keys.Mode);
    }

    [Fact]
    public void GivenPalette_WhenTypingAndBackspacing_ThenTheQueryFollows()
    {
        Press(new Key(':'));

        Press(new Key('t'));
        Press(new Key('a'));
        Press(new Key('b'));
        var typed = _overlays.State.Palette!.Query;
        Press(Key.Backspace);

        Assert.Equal("tab", typed);
        Assert.Equal("ta", _overlays.State.Palette.Query);
    }

    [Fact]
    public void GivenPalette_WhenTypingKeysThatAreBindingsElsewhere_ThenTheyAreTextNotActions()
    {
        Press(new Key(':'));

        Press(new Key('q'));
        Press(new Key(' '));
        Press(new Key('j'));

        Assert.Equal("q j", _overlays.State.Palette!.Query);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenPalette_WhenFilteringAndPressingEnter_ThenTheChosenActionRunsAfterTheCloseAndModeIsRestored()
    {
        Press(new Key(':'));
        foreach (var letter in "jump to head")
        {
            Press(new Key(letter));
        }

        Press(Key.Enter);

        Assert.False(_overlays.State.IsOpen);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Equal([ActionIds.GraphJumpToHead], _routed);
    }

    [Fact]
    public void GivenPalette_WhenMovingWithArrowsAndPressingEnter_ThenTheHighlightedActionRuns()
    {
        Press(new Key(':'));

        Press(Key.CursorDown);
        Press(Key.Enter);

        Assert.Equal(PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView)[1].Action, _routed.Single());
    }

    [Fact]
    public void GivenPaletteWithNoMatch_WhenPressingEnter_ThenNothingRunsAndItCloses()
    {
        Press(new Key(':'));
        Press(new Key('z'));
        Press(new Key('z'));
        Press(new Key('z'));
        Press(new Key('q'));
        Press(new Key('q'));

        Press(Key.Enter);

        Assert.False(_overlays.State.IsOpen);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenPalette_WhenChoosingTheHelpAction_ThenHelpOpensAfterThePaletteClosed()
    {
        Press(new Key(':'));
        foreach (var letter in "show keys")
        {
            Press(new Key(letter));
        }

        Press(Key.Enter);

        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);
    }

    [Fact]
    public void GivenPendingChord_WhenTheDelayElapses_ThenTheWhichKeyPopupShowsAndHidesWhenTheChordEnds()
    {
        Press(new Key('g'));
        Assert.False(_overlays.State.WhichKeyVisible);
        Assert.True(_postedSignal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        RunPosted();

        var shown = _overlays.State.WhichKeyVisible;
        var frame = _overlays.CurrentFrame();
        Press(new Key('g'));

        Assert.True(shown);
        Assert.StartsWith(" g-", frame!.Lines[0].Text, StringComparison.Ordinal);
        Assert.False(_overlays.State.WhichKeyVisible);
        Assert.Null(_overlays.CurrentFrame());
    }

    [Fact]
    public void GivenPendingChord_WhenItIsCancelledBeforeTheDelay_ThenThePopupNeverShows()
    {
        Press(new Key('g'));
        Press(Key.Esc);

        // A sentinel timer started after the cancel and with the same delay fires after any late callback of the cancelled one.
        using var sentinel = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        });
        sentinel.Start(() => { });
        Assert.True(_postedSignal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        RunPosted();

        Assert.False(_overlays.State.WhichKeyVisible);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void GivenAKeyWithNoOverlay_WhenHandlingIt_ThenItIsLeftToTheRouter()
    {
        Press(new Key('j'));

        Assert.Equal([ActionIds.ListDown], _routed);
    }

    [Fact]
    public void GivenPalette_WhenPressingAnUnboundKey_ThenItIsConsumedSoGraphKeysNeverLeakThrough()
    {
        Press(new Key(':'));

        Assert.True(Press(Key.Home));
        Assert.True(Press(Key.Tab));
        Assert.Empty(_routed);
    }
}
