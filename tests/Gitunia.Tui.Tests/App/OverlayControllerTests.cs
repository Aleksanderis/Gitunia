using Gitunia.Core.Status;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Tui.App;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public sealed class OverlayControllerTests : IDisposable
{
    private readonly List<ActionId> routed = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> posted = new();
    private readonly KeyController keys;
    private readonly OverlayController overlays;
    private readonly WhichKeyTimer timer;
    private readonly StatusChannel statuses = new();
    private int changes;

    public OverlayControllerTests()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        OverlayController? controller = null;
        keys = new KeyController(engine, (action, count) => RouteAction(controller!, action, count));
        timer = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), action => posted.Enqueue(action));
        controller = new OverlayController(engine, keys, timer, (action, count) => RouteAction(controller!, action, count), () => (80, 24), GraphGlyphSet.Unicode, () => DefaultKeymap.GraphView, statuses);
        overlays = controller;
        overlays.Changed += () => changes++;
    }

    public void Dispose() => timer.Dispose();

    private void RunPosted()
    {
        while (posted.TryDequeue(out var action))
        {
            action();
        }
    }

    private void RouteAction(OverlayController controller, ActionId action, int? count)
    {
        if (!controller.Handle(action, count))
        {
            routed.Add(action);
        }
    }

    private bool Press(Key key) => keys.Handle(key, overlays.ViewName);

    [Fact]
    public void GivenGraphView_WhenPressingQuestionMark_ThenHelpOpensForTheGraphViewAndNormalModeStays()
    {
        Press(new Key('?'));

        Assert.Equal(OverlayKind.Help, overlays.State.Kind);
        Assert.Equal(DefaultKeymap.GraphView, overlays.State.HelpView);
        Assert.Equal(DefaultKeymap.HelpView, overlays.ViewName);
        Assert.Equal(KeyMode.Normal, keys.Mode);
        Assert.NotNull(overlays.CurrentFrame());
    }

    [Fact]
    public void GivenAnOpenPalette_WhenAClickLandsOnAnEntryLine_ThenThatEntryIsHighlighted()
    {
        Press(new Key(':'));
        var changesBefore = changes;

        var selected = overlays.SelectPaletteEntryAt(4);

        Assert.True(selected);
        Assert.Equal(3, overlays.State.Palette!.SelectedIndex);
        Assert.True(changes > changesBefore);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenAClickLandsOnTheQueryBar_ThenNothingIsHighlighted()
    {
        Press(new Key(':'));

        Assert.False(overlays.SelectPaletteEntryAt(0));
        Assert.Equal(0, overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenNoPalette_WhenAClickArrives_ThenNothingIsHighlighted()
    {
        Press(new Key('?'));

        Assert.False(overlays.SelectPaletteEntryAt(2));
    }

    [Fact]
    public void GivenAHelpOverlayScrolledWithTheWheelAction_WhenAskedToMoveDown_ThenItScrolls()
    {
        Press(new Key('?'));

        overlays.Handle(ActionIds.ListDown, 3);

        Assert.Equal(3, overlays.State.HelpScroll);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenTheWheelActionsArrive_ThenTheHighlightMoves()
    {
        Press(new Key(':'));

        overlays.Handle(ActionIds.PaletteNext, null);
        overlays.Handle(ActionIds.PaletteNext, null);
        overlays.Handle(ActionIds.PalettePrevious, null);

        Assert.Equal(1, overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenOpenHelp_WhenPressingEscQuestionMarkOrQ_ThenItCloses()
    {
        foreach (var close in new[] { Key.Esc, new Key('?'), new Key('q') })
        {
            Press(new Key('?'));
            Press(close);

            Assert.False(overlays.State.IsOpen);
            Assert.Null(overlays.CurrentFrame());
        }

        Assert.Empty(routed);
    }

    [Fact]
    public void GivenOpenHelp_WhenPressingOtherActionKeys_ThenTheyAreSwallowedAndJScrolls()
    {
        Press(new Key('?'));

        Press(new Key('j'));
        Press(Key.Tab);

        Assert.Equal(1, overlays.State.HelpScroll);
        Assert.Empty(routed);
    }

    [Fact]
    public void GivenMessages_WhenPressingSpaceM_ThenTheHistoryOpensNewestFirstAndEscCloses()
    {
        statuses.Error("older failure");
        statuses.Warning("newer warning");

        Press(new Key(' '));
        Press(new Key('m'));
        var opened = overlays.State.Kind;
        var lines = overlays.CurrentFrame()!.Lines.Select(line => line.Text).ToList();
        Press(Key.Esc);

        Assert.Equal(OverlayKind.Messages, opened);
        Assert.Contains("Status messages", lines[0], StringComparison.Ordinal);
        Assert.Contains("newer warning", lines[2], StringComparison.Ordinal);
        Assert.Contains("older failure", lines[3], StringComparison.Ordinal);
        Assert.False(overlays.State.IsOpen);
    }

    [Fact]
    public void GivenNoMessages_WhenOpeningTheHistory_ThenItSaysSo()
    {
        Press(new Key(' '));
        Press(new Key('m'));

        Assert.Contains("no messages yet", string.Join('\n', overlays.CurrentFrame()!.Lines.Select(line => line.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenGraphView_WhenPressingColon_ThenThePaletteOpensInCommandMode()
    {
        Press(new Key(':'));

        Assert.Equal(OverlayKind.Palette, overlays.State.Kind);
        Assert.Equal(KeyMode.Command, keys.Mode);
        Assert.Equal(DefaultKeymap.PaletteView, overlays.ViewName);
    }

    [Fact]
    public void GivenInsertMode_WhenPressingCtrlP_ThenThePaletteOpensAndEscRestoresInsertMode()
    {
        keys.SetMode(KeyMode.Insert);

        Press(Key.P.WithCtrl);
        var opened = overlays.State.Kind;
        Press(Key.Esc);

        Assert.Equal(OverlayKind.Palette, opened);
        Assert.False(overlays.State.IsOpen);
        Assert.Equal(KeyMode.Insert, keys.Mode);
    }

    [Fact]
    public void GivenPalette_WhenTypingAndBackspacing_ThenTheQueryFollows()
    {
        Press(new Key(':'));

        Press(new Key('t'));
        Press(new Key('a'));
        Press(new Key('b'));
        var typed = overlays.State.Palette!.Query;
        Press(Key.Backspace);

        Assert.Equal("tab", typed);
        Assert.Equal("ta", overlays.State.Palette.Query);
    }

    [Fact]
    public void GivenPalette_WhenTypingKeysThatAreBindingsElsewhere_ThenTheyAreTextNotActions()
    {
        Press(new Key(':'));

        Press(new Key('q'));
        Press(new Key(' '));
        Press(new Key('j'));

        Assert.Equal("q j", overlays.State.Palette!.Query);
        Assert.Empty(routed);
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

        Assert.False(overlays.State.IsOpen);
        Assert.Equal(KeyMode.Normal, keys.Mode);
        Assert.Equal([ActionIds.GraphJumpToHead], routed);
    }

    [Fact]
    public void GivenPalette_WhenMovingWithArrowsAndPressingEnter_ThenTheHighlightedActionRuns()
    {
        Press(new Key(':'));

        Press(Key.CursorDown);
        Press(Key.Enter);

        Assert.Equal(PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView)[1].Action, routed.Single());
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

        Assert.False(overlays.State.IsOpen);
        Assert.Empty(routed);
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

        Assert.Equal(OverlayKind.Help, overlays.State.Kind);
    }

    [Fact]
    public void GivenPendingChord_WhenTheDelayElapses_ThenTheWhichKeyPopupShowsAndHidesWhenTheChordEnds()
    {
        Press(new Key('g'));
        Assert.False(overlays.State.WhichKeyVisible);
        Assert.True(SpinWait.SpinUntil(() => !posted.IsEmpty, TimeSpan.FromSeconds(5)));
        RunPosted();

        var shown = overlays.State.WhichKeyVisible;
        var frame = overlays.CurrentFrame();
        Press(new Key('g'));

        Assert.True(shown);
        Assert.StartsWith(" g-", frame!.Lines[0].Text, StringComparison.Ordinal);
        Assert.False(overlays.State.WhichKeyVisible);
        Assert.Null(overlays.CurrentFrame());
    }

    [Fact]
    public void GivenPendingChord_WhenItIsCancelledBeforeTheDelay_ThenThePopupNeverShows()
    {
        Press(new Key('g'));
        Press(Key.Esc);
        Thread.Sleep(50);
        RunPosted();

        Assert.False(overlays.State.WhichKeyVisible);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void GivenAKeyWithNoOverlay_WhenHandlingIt_ThenItIsLeftToTheRouter()
    {
        Press(new Key('j'));

        Assert.Equal([ActionIds.ListDown], routed);
    }

    [Fact]
    public void GivenPalette_WhenPressingAnUnboundKey_ThenItIsConsumedSoGraphKeysNeverLeakThrough()
    {
        Press(new Key(':'));

        Assert.True(Press(Key.Home));
        Assert.True(Press(Key.Tab));
        Assert.Empty(routed);
    }
}
