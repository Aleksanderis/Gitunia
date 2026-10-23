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
    private ScreenSize _screen = new(80, 24);
    private int _changes;
    private string _baseView = DefaultKeymap.GraphView;

    public OverlayControllerTests()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        _keys = new KeyController(engine, RouteAction);
        _timer = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        });

        _overlays = new OverlayController(
            engine,
            _keys,
            _timer,
            RouteAction,
            () => _screen,
            GraphGlyphSet.Unicode,
            () => _baseView,
            _statuses);
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

    private void RouteAction(ActionId action, int? count)
    {
        if (!_overlays.Handle(action, count))
        {
            _routed.Add(action);
        }
    }

    private bool Press(Key key) => _keys.Handle(key, _overlays.ViewName);

    private static ConfirmPrompt Prompt() =>
        new("Discard changes", ["Discard all unstaged changes to a.txt?"], "This cannot be undone.", "Discard changes");

    [Fact]
    public void GivenAConfirmDialog_WhenPressingY_ThenItClosesAndThenRunsTheCallbackOnce()
    {
        var calls = 0;
        var stateWhenCalled = OverlayKind.Confirm;
        _overlays.ShowConfirm(Prompt(), () =>
        {
            calls++;
            stateWhenCalled = _overlays.State.Kind;
        });
        Assert.Equal(KeyMode.Confirm, _keys.Mode);
        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);

        Press(new Key('y'));

        Assert.Equal(1, calls);
        Assert.Equal(OverlayKind.None, stateWhenCalled);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenAnsweringNoOrEscape_ThenTheCallbackNeverRuns()
    {
        var calls = 0;
        foreach (var answer in new[] { new Key('n'), Key.Esc })
        {
            _overlays.ShowConfirm(Prompt(), () => calls++);

            Press(answer);

            Assert.Equal(OverlayKind.None, _overlays.State.Kind);
            Assert.Equal(KeyMode.Normal, _keys.Mode);
        }

        Assert.Equal(0, calls);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenOtherKeysArrive_ThenTheyAreSwallowedAndNothingConfirms()
    {
        var calls = 0;
        _overlays.ShowConfirm(Prompt(), () => calls++);

        foreach (var key in new[] { Key.Enter, new Key('j'), new Key('q'), Key.CursorDown, new Key(' ') })
        {
            Assert.True(Press(key));
        }

        Assert.Equal(0, calls);
        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenAVisualRange_WhenAConfirmDialogCloses_ThenVisualModeIsRestored()
    {
        _keys.SetMode(KeyMode.Visual);
        _overlays.ShowConfirm(Prompt(), () => { });

        Press(new Key('n'));

        Assert.Equal(KeyMode.Visual, _keys.Mode);
    }

    [Fact]
    public void GivenAScreenTooSmallForTheDialog_WhenAsking_ThenNothingOpensAndTheCallerIsTold()
    {
        _screen = new ScreenSize(14, 24);

        var shown = _overlays.ShowConfirm(Prompt(), () => { });

        Assert.False(shown);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheScreenShrinksBelowIt_ThenItClosesUnansweredAndAStrayYDoesNothing()
    {
        var calls = 0;
        _overlays.ShowConfirm(Prompt(), () => calls++);
        _screen = new ScreenSize(14, 24);

        _overlays.OnScreenResized();
        Press(new Key('y'));

        Assert.Equal(0, calls);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Contains("nothing was done", _statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheScreenResizesAndStillFits_ThenItStaysOpen()
    {
        _overlays.ShowConfirm(Prompt(), () => { });
        _screen = new ScreenSize(100, 30);

        _overlays.OnScreenResized();

        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheYesAndNoRowsAreClicked_ThenTheyAnswerLikeTheKeys()
    {
        _overlays.ShowConfirm(Prompt(), () => { });
        var height = _overlays.CurrentFrame()!.Height;

        Assert.Equal(ConfirmAnswer.Yes, _overlays.ConfirmAnswerAt(height - 3));
        Assert.Equal(ConfirmAnswer.No, _overlays.ConfirmAnswerAt(height - 2));
        Assert.Null(_overlays.ConfirmAnswerAt(0));
        Assert.Null(_overlays.ConfirmAnswerAt(height - 1));
    }

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
        Assert.Equal(2, _overlays.State.Palette!.SelectedIndex);
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
    public void GivenAnOpenPalette_WhenPressingCtrlJAndCtrlK_ThenTheHighlightMovesDownAndUp()
    {
        Press(new Key(':'));

        Press(Key.J.WithCtrl);
        Press(Key.J.WithCtrl);
        Press(Key.K.WithCtrl);

        Assert.Equal(1, _overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenAnOpenPalette_WhenPressingCtrlH_ThenTheHighlightStays()
    {
        Press(new Key(':'));
        Press(Key.J.WithCtrl);

        Press(Key.H.WithCtrl);

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
    public void GivenAGraphRow_WhenPressingTheMenuKey_ThenAnActionsMenuOpensWithTheRowActionsAndTheirKeys()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));

        Press(new Key(' '));
        Press(new Key('.'));

        Assert.Equal(OverlayKind.ContextMenu, _overlays.State.Kind);
        Assert.Equal(DefaultKeymap.ContextMenuView, _overlays.ViewName);
        var entries = _overlays.State.Palette!.Entries;
        Assert.Equal(RowActions.OfGraphRow, entries.Select(entry => entry.Action));
        Assert.Equal("<space>bs", entries.Single(entry => entry.Action == ActionIds.BranchSwitch).Keys);
        Assert.Empty(_routed);
    }

    [Theory]
    [InlineData(DefaultKeymap.DiffView)]
    [InlineData(DefaultKeymap.WorkingUnstagedDiffView)]
    [InlineData(DefaultKeymap.WorkingStagedDiffView)]
    public void GivenADiffView_WhenOpeningTheViewDropdown_ThenItListsTheViewOptionsWithTheirKeysUnderTheViewTitle(
        string view)
    {
        _baseView = view;
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.ViewOptions,
            new MenuAnchor(60, 0),
            RowActions.ViewOptionsTitle);

        Assert.True(_overlays.Handle(ActionIds.DiffOpenViewOptions, null));

        Assert.Equal(OverlayKind.ContextMenu, _overlays.State.Kind);
        Assert.Equal("View", _overlays.State.MenuTitle);
        var entries = _overlays.State.Palette!.Entries;
        Assert.Equal(RowActions.ViewOptions, entries.Select(entry => entry.Action));
        Assert.Equal("<space>vf", entries.Single(entry => entry.Action == ActionIds.DiffToggleWholeFile).Keys);
        Assert.Equal("<space>vw", entries.Single(entry => entry.Action == ActionIds.DiffToggleWrap).Keys);
    }

    [Fact]
    public void GivenTheViewDropdown_WhenPressingEnterOrEsc_ThenTheOptionRunsAfterTheCloseOrNothingRuns()
    {
        _baseView = DefaultKeymap.DiffView;
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.ViewOptions,
            new MenuAnchor(60, 0),
            RowActions.ViewOptionsTitle);
        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);
        Press(Key.Esc);

        Assert.False(_overlays.State.IsOpen);
        Assert.Empty(_routed);

        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);
        Press(Key.CursorDown);
        Press(Key.Enter);

        Assert.False(_overlays.State.IsOpen);
        Assert.Equal([RowActions.ViewOptions[1]], _routed);
    }

    [Fact]
    public void GivenNoTargetForTheViewDropdown_WhenOpeningIt_ThenTheStatusBarSaysThereAreNoViewOptions()
    {
        _baseView = DefaultKeymap.DiffView;

        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("No view options", _statuses.Log.History[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScreenTooSmallForTheDropdown_WhenOpeningIt_ThenTheStatusBarSaysSoAndNothingOpens()
    {
        _baseView = DefaultKeymap.DiffView;
        _screen = new ScreenSize(10, 2);
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.ViewOptions,
            new MenuAnchor(0, 0),
            RowActions.ViewOptionsTitle);

        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("too small", _statuses.Log.History[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnActionsMenu_WhenMovingAndPressingEnter_ThenTheHighlightedActionRunsAfterTheClose()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));
        _overlays.Handle(ActionIds.MenuOpenForRow, null);

        Press(new Key('j'));
        Press(Key.CursorDown);
        Press(new Key('k'));
        Press(Key.Enter);

        Assert.False(_overlays.State.IsOpen);
        Assert.Equal([RowActions.OfGraphRow[1]], _routed);
    }

    [Fact]
    public void GivenAnActionsMenu_WhenTypingText_ThenNothingFiltersAndEscCloses()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));
        _overlays.Handle(ActionIds.MenuOpenForRow, null);
        Press(new Key('x'));
        var query = _overlays.State.Palette!.Query;
        Press(Key.Esc);

        Assert.Equal(string.Empty, query);
        Assert.False(_overlays.State.IsOpen);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenAnActionsMenu_WhenClickingAnEntry_ThenItIsHighlightedAndABorderLineIsNot()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));
        _overlays.Handle(ActionIds.MenuOpenForRow, null);

        Assert.False(_overlays.SelectMenuEntryAt(0));
        Assert.False(_overlays.SelectMenuEntryAt(1));
        Assert.True(_overlays.SelectMenuEntryAt(3));

        Assert.Equal(1, _overlays.State.Palette!.SelectedIndex);
    }

    [Fact]
    public void GivenAnActionsMenu_WhenTheScreenIsResized_ThenItCloses()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));
        _overlays.Handle(ActionIds.MenuOpenForRow, null);

        _screen = new ScreenSize(100, 30);
        _overlays.OnScreenResized();

        Assert.False(_overlays.State.IsOpen);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
    }

    [Fact]
    public void GivenNoRowToActOn_WhenOpeningTheMenu_ThenNothingOpens()
    {
        Assert.True(_overlays.Handle(ActionIds.MenuOpenForRow, null));

        Assert.False(_overlays.State.IsOpen);
    }

    [Fact]
    public void GivenARowWithNoKeyedAction_WhenOpeningTheMenu_ThenTheStatusSaysThereAreNone()
    {
        _overlays.MenuTarget = _ => new RowMenuTarget([], new MenuAnchor(0, 0));

        _overlays.Handle(ActionIds.MenuOpenForRow, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("No actions", _statuses.Log.History[^1].Text, StringComparison.Ordinal);
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
    public void GivenPalette_WhenMovingWithCtrlJAndPressingCtrlL_ThenTheHighlightedActionRunsLikeEnter()
    {
        Press(new Key(':'));

        Press(Key.J.WithCtrl);
        Press(Key.L.WithCtrl);

        Assert.Equal(PaletteEntryBuilder.Build(DefaultKeymap.Create(), DefaultKeymap.GraphView)[1].Action, _routed.Single());
        Assert.False(_overlays.State.IsOpen);
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
