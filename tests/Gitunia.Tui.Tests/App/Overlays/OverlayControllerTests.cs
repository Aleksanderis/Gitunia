using Gitunia.Tui.App;
using System.Collections.Concurrent;
using Gitunia.App.Shell;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Tests.Fakes;
using Gitunia.Tui.Views;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.App.Overlays;

public sealed class OverlayControllerTests : IDisposable, IActionRoute
{
    private readonly List<ActionId> _routed = [];
    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly TestSignal _postedSignal = new();
    private readonly KeyController _keys;
    private readonly OverlayHarness _overlays;
    private readonly WhichKeyTimer _timer;
    private readonly TestShell _shell = new();
    private readonly FakePaneChrome _chrome = new(new ScreenSize(80, 24));
    private int _changes;
    private string _baseView = DefaultKeymap.GraphView;
    private CommandLevel _level = CommandLevel.Everyday;

    public OverlayControllerTests()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        _keys = new KeyController(engine, this, _shell.Host);
        _timer = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), new DelegateUi(action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        }));

        _overlays = new OverlayHarness(
            engine,
            _keys,
            _timer,
            this,
            _chrome,
            new TestFocusedView(() => _baseView),
            _shell);
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

    void IActionRoute.Route(ActionId action, int? count)
    {
        if (!_overlays.Handle(action, count))
        {
            _routed.Add(action);
        }
    }

    private void Resize(int width, int height)
    {
        _chrome.ScreenSize = new ScreenSize(width, height);
        _shell.Host.Dispatch(
            new ScreenResizedCommand(new ScreenExtent(width, height), _shell.Host.Clock.GetUtcNow()));
    }

    private string FrameText() => string.Join('\n', _overlays.CurrentFrame()!.Lines.Select(line => line.Text));

    private bool Press(Key key) => _keys.Handle(key, _overlays.ViewName);

    private static ConfirmPrompt Prompt() =>
        new("Discard changes", ["Discard all unstaged changes to a.txt?"], "This cannot be undone.", "Discard changes");

    [Fact]
    public void GivenAConfirmDialog_WhenPressingY_ThenItClosesAndThenRunsTheCallbackOnce()
    {
        _shell.Confirm(Prompt());
        Assert.Equal(KeyMode.Confirm, _keys.Mode);
        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);

        Press(new Key('y'));

        Assert.Single(_shell.Accepted);
        Assert.Equal([false], _shell.AcceptedWhileAsking);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
    }

    [Fact]
    public void GivenOutput_WhenShown_ThenTheBoxOpensInNormalModeAndEscClosesItAndRestoresTheMode()
    {
        _shell.Host.SetKeyMode(KeyMode.Insert);

        _shell.Host.ShowOutput("Commit failed", "Hook output", ["first line", "second line"]);

        Assert.Equal(OverlayKind.Output, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Contains("second line", FrameText(), StringComparison.Ordinal);

        Press(Key.Esc);

        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Null(_shell.Host.State.Shell.Output);
        Assert.Equal(KeyMode.Insert, _keys.Mode);
    }

    [Fact]
    public void GivenAnOpenHelp_WhenAConfirmReplacesItAndAnotherOneClosesByEsc_ThenTheShellStateFollowsEachStep()
    {
        Press(new Key('?'));
        Assert.Equal(OverlayKind.Help, _shell.Host.State.Shell.Overlay);

        _shell.Confirm(Prompt());
        Assert.Equal(OverlayKind.None, _shell.Host.State.Shell.Overlay);

        Press(new Key('n'));
        Press(new Key('?'));
        Press(Key.Esc);
        Assert.Equal(OverlayKind.None, _shell.Host.State.Shell.Overlay);
    }

    [Fact]
    public void GivenAnOpenHelp_WhenOutputIsShown_ThenTheOutputReplacesIt()
    {
        Press(new Key('?'));
        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);

        _shell.Host.ShowOutput("Commit failed", "Hook output", ["line"]);

        Assert.Equal(OverlayKind.Output, _overlays.State.Kind);
    }

    [Fact]
    public void GivenAnOpenHelp_WhenAConfirmIsAsked_ThenTheDialogReplacesItAndNIsTheAnswer()
    {
        Press(new Key('?'));

        _shell.Confirm(Prompt());
        Press(new Key('n'));

        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Null(_shell.Host.State.Shell.Confirm);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenYIsPressed_ThenTheShellStateHoldsNoDialogAndTheChangeIsAnnounced()
    {
        _shell.Confirm(Prompt());
        var changes = _changes;

        Press(new Key('y'));

        Assert.Null(_shell.Host.State.Shell.Confirm);
        Assert.True(_changes > changes);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheShellOpensIt_ThenTheViewIsTheConfirmViewAndTheFrameShowsIt()
    {
        _shell.Confirm(Prompt());

        Assert.Equal(DefaultKeymap.ConfirmView, _overlays.ViewName);
        Assert.Contains("Discard all unstaged changes", FrameText(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenAnsweringNoOrEscape_ThenTheCallbackNeverRuns()
    {
        foreach (var answer in new[] { new Key('n'), Key.Esc })
        {
            _shell.Confirm(Prompt());

            Press(answer);

            Assert.Equal(OverlayKind.None, _overlays.State.Kind);
            Assert.Equal(KeyMode.Normal, _keys.Mode);
        }

        Assert.Empty(_shell.Accepted);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenOtherKeysArrive_ThenTheyAreSwallowedAndNothingConfirms()
    {
        _shell.Confirm(Prompt());

        foreach (var key in new[] { Key.Enter, new Key('j'), new Key('q'), Key.CursorDown, new Key(' ') })
        {
            Assert.True(Press(key));
        }

        Assert.Empty(_shell.Accepted);
        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenAVisualRange_WhenAConfirmDialogCloses_ThenVisualModeIsRestored()
    {
        _shell.Host.SetKeyMode(KeyMode.Visual);
        _shell.Confirm(Prompt());

        Press(new Key('n'));

        Assert.Equal(KeyMode.Visual, _keys.Mode);
    }

    [Fact]
    public void GivenAScreenTooSmallForTheDialog_WhenAsking_ThenNothingOpensAndTheCallerIsTold()
    {
        Resize(14, 24);

        var shown = _shell.Confirm(Prompt());

        Assert.False(shown);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheScreenShrinksBelowIt_ThenItClosesUnansweredAndAStrayYDoesNothing()
    {
        _shell.Confirm(Prompt());
        Resize(14, 24);
        _overlays.OnScreenResized();
        Press(new Key('y'));

        Assert.Empty(_shell.Accepted);
        Assert.Equal(OverlayKind.None, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Contains("nothing was done", _shell.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheScreenResizesAndStillFits_ThenItStaysOpen()
    {
        _shell.Confirm(Prompt());
        Resize(100, 30);
        _overlays.OnScreenResized();

        Assert.Equal(OverlayKind.Confirm, _overlays.State.Kind);
    }

    [Fact]
    public void GivenAConfirmDialog_WhenTheYesAndNoRowsAreClicked_ThenTheyAnswerLikeTheKeys()
    {
        _shell.Confirm(Prompt());
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
    public void GivenOpenHelp_WhenPressingOtherActionKeys_ThenTheyAreSwallowedAndShiftJScrolls()
    {
        Press(new Key('?'));

        Press(new Key('J'));
        Press(new Key('x'));

        Assert.Equal(1, _overlays.State.HelpScroll);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenOpenHelp_WhenTypingSlashAndAKeySequence_ThenOnlyTheMatchingRowsShowAndTheTitleCarriesTheFilter()
    {
        Press(new Key('?'));

        Press(new Key('/'));
        foreach (var character in "<space>gc")
        {
            Press(new Key(character));
        }

        Assert.Equal(KeyMode.Insert, _keys.Mode);
        Assert.Equal("<space>gc", _overlays.State.HelpFilter);
        var text = FrameText();
        Assert.Contains("/ <space>gc_", text);
        Assert.Contains("<space>gc", text);
        Assert.DoesNotContain("<space>gd", text);
        Assert.Empty(_routed);
    }

    [Fact]
    public void GivenAFilteredHelp_WhenClickingTheMenuArea_ThenNoSectionIsPicked()
    {
        Press(new Key('?'));
        Press(new Key('/'));
        Press(new Key('g'));

        Assert.Null(_overlays.HelpTabNumberAt(2, 5));
    }

    [Fact]
    public void GivenAHelpFilter_WhenTypingAnActionIdOrBackspacing_ThenRowsFollowTheText()
    {
        Press(new Key('?'));
        Press(new Key('/'));
        foreach (var character in "help.tab")
        {
            Press(new Key(character));
        }

        Press(Key.Backspace);

        Assert.Equal("help.ta", _overlays.State.HelpFilter);
        Assert.Contains("Nothing matches the filter.", FrameText());
    }

    [Fact]
    public void GivenAHelpFilterKept_WhenPressingEnter_ThenTypingStopsJScrollsAndEscClearsBeforeItCloses()
    {
        Press(new Key('?'));
        Press(new Key('/'));
        Press(new Key('g'));

        Press(Key.Enter);

        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Equal("g", _overlays.State.HelpFilter);
        Press(new Key('j'));
        Assert.Equal(1, _overlays.State.HelpScroll);
        Press(new Key('l'));
        Assert.Equal(0, _overlays.State.HelpTab);

        Press(Key.Esc);

        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);
        Assert.Equal(string.Empty, _overlays.State.HelpFilter);
        Press(Key.Esc);
        Assert.False(_overlays.State.IsOpen);
    }

    [Fact]
    public void GivenTheFilterLineIsBeingTyped_WhenPressingEsc_ThenItClearsAndTheHelpStaysOpen()
    {
        Press(new Key('?'));
        Press(new Key('/'));
        Press(new Key('q'));

        Press(Key.Esc);

        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);
        Assert.Equal(KeyMode.Normal, _keys.Mode);
        Assert.Equal(string.Empty, _overlays.State.HelpFilter);
        Assert.False(_overlays.State.IsEditingHelpFilter);
    }

    [Fact]
    public void GivenOpenHelp_WhenPressingTabAndShiftTab_ThenTheFourGraphTabsCycleBothWaysAndScrollStartsOver()
    {
        Press(new Key('?'));
        Press(new Key('J'));

        Press(Key.Tab);
        var second = _overlays.State;
        Press(new Key('j'));
        Press(new Key('j'));
        var wrapped = _overlays.State.HelpTab;
        Press(new Key('k'));
        Press(Key.Tab.WithShift);

        Assert.Equal(1, second.HelpTab);
        Assert.Equal(0, second.HelpScroll);
        Assert.Equal(3, wrapped);
        Assert.Equal(1, _overlays.State.HelpTab);
        Assert.Empty(_routed);
        Assert.Equal(OverlayKind.Help, _overlays.State.Kind);
    }

    [Fact]
    public void GivenOpenHelp_WhenClickingTheSecondMenuEntry_ThenThatSectionShows()
    {
        Press(new Key('?'));

        var number = _overlays.HelpTabNumberAt(2, 5);
        _overlays.Handle(ActionIds.HelpTabSelect, number);

        Assert.Equal(2, number);
        Assert.Equal(1, _overlays.State.HelpTab);
        Assert.Null(_overlays.HelpTabNumberAt(2, 60));
        Assert.Null(_overlays.HelpTabNumberAt(1, 0));
    }

    [Fact]
    public void GivenMessages_WhenPressingSpaceM_ThenTheHistoryOpensNewestFirstAndEscCloses()
    {
        _shell.Statuses.Error("older failure");
        _shell.Statuses.Warning("newer warning");

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
        _shell.Statuses.Error("first failure");
        Press(new Key(' '));
        Press(new Key('m'));
        var changes = 0;
        _overlays.Changed += () => changes++;

        _shell.Statuses.Warning("late warning");
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
        Assert.Equal("<space>z", entries.Single(entry => entry.Action == ActionIds.PaneZoom).Keys);
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

    [Theory]
    [InlineData(DefaultKeymap.DetailChangesView)]
    [InlineData(DefaultKeymap.WorkingUnstagedView)]
    [InlineData(DefaultKeymap.WorkingStagedView)]
    public void GivenAFileListView_WhenOpeningItsViewDropdown_ThenItListsThePathStylesUnderTheViewTitle(string view)
    {
        _baseView = view;
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.PathStyleOptions,
            new MenuAnchor(60, 0),
            RowActions.ListViewOptionsTitle);

        Assert.True(_overlays.Handle(ActionIds.ListOpenViewOptions, null));

        Assert.Equal(OverlayKind.ContextMenu, _overlays.State.Kind);
        Assert.Equal("View", _overlays.State.MenuTitle);
        Assert.Equal(RowActions.PathStyleOptions, _overlays.State.Palette!.Entries.Select(entry => entry.Action));
        Press(Key.CursorDown);
        Press(Key.CursorDown);
        Press(Key.Enter);
        Assert.Equal([ActionIds.FilesPathTree], _routed);
    }

    [Fact]
    public void GivenTheFilesTab_WhenOpeningItsViewDropdown_ThenItOffersTheFilterWithItsKey()
    {
        _baseView = DefaultKeymap.DetailFilesView;
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.FilesTabViewOptions,
            new MenuAnchor(60, 0),
            RowActions.ListViewOptionsTitle);

        _overlays.Handle(ActionIds.ListOpenViewOptions, null);

        var entry = Assert.Single(_overlays.State.Palette!.Entries);
        Assert.Equal(ActionIds.DetailFilter, entry.Action);
        Assert.Equal("/", entry.Keys);
    }

    [Fact]
    public void GivenNoTargetForTheListDropdown_WhenOpeningIt_ThenTheStatusBarSaysThereAreNoViewOptions()
    {
        _baseView = DefaultKeymap.DetailView;

        _overlays.Handle(ActionIds.ListOpenViewOptions, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("No view options", _shell.Statuses.Log.History[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoTargetForTheViewDropdown_WhenOpeningIt_ThenTheStatusBarSaysThereAreNoViewOptions()
    {
        _baseView = DefaultKeymap.DiffView;

        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("No view options", _shell.Statuses.Log.History[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAScreenTooSmallForTheDropdown_WhenOpeningIt_ThenTheStatusBarSaysSoAndNothingOpens()
    {
        _baseView = DefaultKeymap.DiffView;
        _chrome.ScreenSize = new ScreenSize(10, 2);
        _overlays.MenuTarget = _ => new RowMenuTarget(
            RowActions.ViewOptions,
            new MenuAnchor(0, 0),
            RowActions.ViewOptionsTitle);

        _overlays.Handle(ActionIds.DiffOpenViewOptions, null);

        Assert.False(_overlays.State.IsOpen);
        Assert.Contains("too small", _shell.Statuses.Log.History[^1].Text, StringComparison.Ordinal);
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

        _chrome.ScreenSize = new ScreenSize(100, 30);
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
        Assert.Contains("No actions", _shell.Statuses.Log.History[^1].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenInsertMode_WhenPressingCtrlP_ThenThePaletteOpensAndEscRestoresInsertMode()
    {
        _shell.Host.SetKeyMode(KeyMode.Insert);

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
    public void GivenEveryday_WhenOpeningThePalette_ThenAnAdvancedActionIsNotListedButItsExactIdStillRuns()
    {
        _overlays.Level = () => CommandLevel.Everyday;
        Press(new Key(':'));

        Assert.DoesNotContain(_overlays.State.Palette!.Entries, entry => entry.Action == ActionIds.ReflogOpen);
        foreach (var letter in "reflog.open")
        {
            Press(new Key(letter));
        }

        Press(Key.Enter);

        Assert.Equal([ActionIds.ReflogOpen], _routed);
    }

    [Fact]
    public void GivenEveryday_WhenTypingAnIdThatIsOnlyAPrefixOfListedRows_ThenEnterRunsTheHighlightedRowNotAGuess()
    {
        _overlays.Level = () => CommandLevel.Everyday;
        Press(new Key(':'));
        foreach (var letter in "jump to head")
        {
            Press(new Key(letter));
        }

        Press(Key.Enter);

        Assert.Equal([ActionIds.GraphJumpToHead], _routed);
    }

    [Theory]
    [InlineData(DefaultKeymap.GraphView, false)]
    [InlineData(DefaultKeymap.ReflogView, true)]
    public void GivenEveryday_WhenTheLeaderIsPending_ThenAnAdvancedPlusKeyShowsOnlyInAModalScreen(
        string view,
        bool expectShown)
    {
        _overlays.Level = () => CommandLevel.Everyday;
        _baseView = view;
        Press(new Key(' '));
        Assert.True(_postedSignal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        RunPosted();

        var frame = string.Join('\n', _overlays.CurrentFrame()!.Lines.Select(line => line.Text));

        Assert.Equal(
            expectShown,
            frame.Contains(ActionCatalog.Describe(ActionIds.ReflogOpen), StringComparison.Ordinal));
    }

    [Fact]
    public void GivenTheLevelChangesWhileTheAppRuns_WhenOpeningThePaletteAgain_ThenItFollowsTheNewLevelAtOnce()
    {
        _level = CommandLevel.Everyday;
        _overlays.Level = () => _level;
        Press(new Key(':'));
        var everyday = _overlays.State.Palette!.Entries.Count;
        Press(Key.Esc);

        _level = CommandLevel.AdvancedPlus;
        Press(new Key(':'));

        Assert.True(_overlays.State.Palette!.Entries.Count > everyday);
        Assert.Contains(_overlays.State.Palette.Entries, entry => entry.Action == ActionIds.ReflogOpen);
    }

    [Fact]
    public void GivenEachLevel_WhenOpeningTheHelpAtItsLastTab_ThenTheClosingLineNamesTheHiddenCommands()
    {
        foreach (var (level, hidden) in new[]
        {
            (CommandLevel.Everyday, true),
            (CommandLevel.Advanced, true),
            (CommandLevel.AdvancedPlus, false),
        })
        {
            _overlays.Level = () => level;
            Press(new Key('?'));
            _overlays.Handle(ActionIds.HelpTabPrevious, null);
            _overlays.Handle(ActionIds.ListBottom, null);

            var frame = string.Join('\n', _overlays.CurrentFrame()!.Lines.Select(line => line.Text));

            Assert.Equal(hidden, frame.Contains("advanced command", StringComparison.Ordinal));
            Press(Key.Esc);
        }
    }

    [Fact]
    public void GivenEveryday_WhenOpeningTheActionsMenuOfAGraphRow_ThenAdvancedActionsAreLeftOut()
    {
        _overlays.Level = () => CommandLevel.Everyday;
        _overlays.MenuTarget = _ => new RowMenuTarget(RowActions.OfGraphRow, new MenuAnchor(3, 5));

        Press(new Key(' '));
        Press(new Key('.'));

        var entries = _overlays.State.Palette!.Entries;
        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.BranchCreateOnly);
        Assert.Contains(entries, entry => entry.Action == ActionIds.BranchSwitch);
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
    public void GivenWhichKeyOff_WhenAChordStaysPending_ThenNoPopupShowsAndTheChordStillCompletes()
    {
        _overlays.WhichKeyEnabled = false;
        Press(new Key('g'));

        // A sentinel timer started after the chord with the same delay fires after any callback of a started wait.
        using var sentinel = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), new DelegateUi(action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        }));
        sentinel.Start(() => { });
        Assert.True(_postedSignal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        RunPosted();

        Assert.False(_overlays.State.WhichKeyVisible);
        Assert.True(_keys.State.HasPending);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void GivenAVisiblePopup_WhenTurningWhichKeyOff_ThenItClosesAtOnceAndTheChordStaysPending()
    {
        Press(new Key('g'));
        Assert.True(_postedSignal.Wait(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
        RunPosted();
        Assert.True(_overlays.State.WhichKeyVisible);

        _overlays.WhichKeyEnabled = false;

        Assert.False(_overlays.State.WhichKeyVisible);
        Assert.True(_keys.State.HasPending);
    }

    [Fact]
    public void GivenPendingChord_WhenItIsCancelledBeforeTheDelay_ThenThePopupNeverShows()
    {
        Press(new Key('g'));
        Press(Key.Esc);

        // A sentinel timer started after the cancel and with the same delay fires after any late callback of the cancelled one.
        using var sentinel = new WhichKeyTimer(TimeSpan.FromMilliseconds(1), new DelegateUi(action =>
        {
            _posted.Enqueue(action);
            _postedSignal.Release();
        }));
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
