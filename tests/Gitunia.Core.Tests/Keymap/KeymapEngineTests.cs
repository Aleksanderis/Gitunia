using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class KeymapEngineTests
{
    private static readonly string[] TypedKeys = ["j", "g", "<space>", "q", "5", "?", "G"];

    private static KeyFeeder CreateFeeder(string view = DefaultKeymap.GraphView) => new(new KeymapEngine(DefaultKeymap.Create()), view);

    [Fact]
    public void GivenNormalMode_WhenPressingASingleKeyBinding_ThenTheActionFiresWithoutCount()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("j");

        Assert.Equal(KeyOutcome.Action, result.Outcome);
        Assert.Equal(ActionIds.ListDown, result.Action);
        Assert.Null(result.Count);
    }

    [Fact]
    public void GivenChord_WhenPressingFirstKey_ThenItIsPendingAndSecondKeyCompletesIt()
    {
        var feeder = CreateFeeder();

        var first = feeder.Feed("g");
        var second = feeder.Feed("g");

        Assert.Equal(KeyOutcome.Pending, first.Outcome);
        Assert.True(first.State.HasPending);
        Assert.Equal(KeyOutcome.Action, second.Outcome);
        Assert.Equal(ActionIds.ListTop, second.Action);
        Assert.False(second.State.HasPending);
    }

    [Fact]
    public void GivenPendingChord_WhenContinuingLater_ThenItStillCompletesBecauseThereIsNoTimeout()
    {
        var feeder = CreateFeeder();
        feeder.Feed("g");

        var continued = feeder.Feed("t");

        Assert.Equal(ActionIds.TabNext, continued.Action);
    }

    [Fact]
    public void GivenPendingChord_WhenPressingEsc_ThenItIsCancelledAndNothingFires()
    {
        var feeder = CreateFeeder();
        feeder.Feed("g");

        var result = feeder.Feed(new KeyEvent(KeyNames.Escape));

        Assert.Equal(KeyOutcome.Cancelled, result.Outcome);
        Assert.False(result.State.HasPending);
        Assert.Equal(ActionIds.ListDown, feeder.Feed("j").Action);
    }

    [Fact]
    public void GivenPendingChord_WhenPressingAKeyThatContinuesNothing_ThenTheChordIsDiscarded()
    {
        var feeder = CreateFeeder();
        feeder.Feed("g");

        var result = feeder.Feed("x");

        Assert.Equal(KeyOutcome.Discarded, result.Outcome);
        Assert.False(result.State.HasPending);
    }

    [Fact]
    public void GivenNoPending_WhenPressingAnUnboundKey_ThenItIsUnbound()
    {
        var feeder = CreateFeeder();

        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("x").Outcome);
    }

    [Fact]
    public void GivenACountPrefix_WhenPressingJ_ThenTheActionCarriesTheCount()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("5j");

        Assert.Equal(ActionIds.ListDown, result.Action);
        Assert.Equal(5, result.Count);
        Assert.Null(feeder.State.Count);
    }

    [Fact]
    public void GivenMultiDigitCount_WhenPressingJ_ThenDigitsAreCombinedAndZeroContinuesTheCount()
    {
        var feeder = CreateFeeder();

        Assert.Equal(12, feeder.Feed("12j").Count);
        Assert.Equal(10, feeder.Feed("10j").Count);
    }

    [Fact]
    public void GivenCountBeforeAChord_WhenCompletingTheChord_ThenTheCountIsKept()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("3gt");

        Assert.Equal(ActionIds.TabNext, result.Action);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void GivenCountInProgress_WhenPressingEsc_ThenTheCountIsDropped()
    {
        var feeder = CreateFeeder();
        feeder.Feed("4");

        var cancelled = feeder.Feed(new KeyEvent(KeyNames.Escape));

        Assert.Equal(KeyOutcome.Cancelled, cancelled.Outcome);
        Assert.Null(feeder.Feed("j").Count);
    }

    [Fact]
    public void GivenALeadingZero_WhenNoCountIsPending_ThenItIsNotACount()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("0");

        Assert.Equal(KeyOutcome.Unbound, result.Outcome);
        Assert.Null(feeder.State.Count);
    }

    [Fact]
    public void GivenAnAbsurdlyLongCount_WhenTyped_ThenItIsClamped()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("99999999999j");

        Assert.Equal(999_999, result.Count);
    }

    [Fact]
    public void GivenAltDigit_WhenPressed_ThenItIsATabBindingNotACount()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("Alt-3");

        Assert.Equal(ActionIds.TabGoTo(3), result.Action);
        Assert.Null(result.Count);
    }

    [Fact]
    public void GivenControlWChord_WhenPressingDirection_ThenFocusActionFires()
    {
        var feeder = CreateFeeder();

        var result = feeder.Feed("Ctrl-w h");

        Assert.Equal(ActionIds.PaneFocusLeft, result.Action);
    }

    [Fact]
    public void GivenLeaderChord_WhenPressingSpaceThenO_ThenPickerOpens()
    {
        var feeder = CreateFeeder();

        Assert.Equal(ActionIds.PickerOpen, feeder.Feed("<space>o").Action);
    }

    [Fact]
    public void GivenViewSpecificBinding_WhenPressedInAnotherView_ThenItDoesNotFire()
    {
        var feeder = CreateFeeder(DefaultKeymap.SidebarView);

        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("@").Outcome);
        Assert.Equal(ActionIds.SidebarFilter, feeder.Feed("/").Action);
    }

    [Fact]
    public void GivenViewBindingShadowingAParent_WhenPressed_ThenTheMoreSpecificOneWins()
    {
        var graph = CreateFeeder();
        var detail = CreateFeeder(DefaultKeymap.DetailView);

        Assert.Equal(ActionIds.Cancel, graph.Feed(new KeyEvent(KeyNames.Escape)).Action);
        Assert.Equal(ActionIds.NavigateBack, detail.Feed(new KeyEvent(KeyNames.Escape)).Action);
    }

    [Fact]
    public void GivenInsertMode_WhenTypingPrintableKeys_ThenTheyAreTextAndNeverBindings()
    {
        var feeder = CreateFeeder(DefaultKeymap.PickerView);
        feeder.State = feeder.State.WithMode(KeyMode.Insert);

        var typed = TypedKeys.Select(keys => feeder.Feed(keys)).ToList();

        Assert.All(typed, result => Assert.Equal(KeyOutcome.TextInput, result.Outcome));
        Assert.Equal("jg q5?G", string.Concat(typed.Select(result => result.Text)));
    }

    [Fact]
    public void GivenInsertMode_WhenPressingAltAndCtrlCombos_ThenTheyActLikeBindings()
    {
        var feeder = CreateFeeder(DefaultKeymap.PickerView);
        feeder.State = feeder.State.WithMode(KeyMode.Insert);

        Assert.Equal(ActionIds.PickerPin, feeder.Feed("Alt-p").Action);
        Assert.Equal(ActionIds.PickerOpen, feeder.Feed("Ctrl-t").Action);
        Assert.Equal(ActionIds.TabGoTo(2), feeder.Feed("Alt-2").Action);
        Assert.Equal(ActionIds.PickerNext, feeder.Feed("Ctrl-j").Action);
        Assert.Equal(ActionIds.PickerPrevious, feeder.Feed("Ctrl-k").Action);
        Assert.Equal(ActionIds.PickerAccept, feeder.Feed("Enter").Action);
        Assert.Equal(ActionIds.Cancel, feeder.Feed("Esc").Action);
    }

    [Fact]
    public void GivenInsertMode_WhenPressingUnboundEditingKey_ThenItIsPassedOn()
    {
        var feeder = CreateFeeder(DefaultKeymap.PickerView);
        feeder.State = feeder.State.WithMode(KeyMode.Insert);

        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("Backspace").Outcome);
    }

    [Fact]
    public void GivenCommandMode_WhenTyping_ThenPrintableKeysAreTextAndEnterExecutes()
    {
        var feeder = CreateFeeder();
        feeder.State = feeder.State.WithMode(KeyMode.Command);

        Assert.Equal(KeyOutcome.TextInput, feeder.Feed("t").Outcome);
        Assert.Equal(ActionIds.CommandExecute, feeder.Feed("Enter").Action);
    }

    [Fact]
    public void GivenConfirmMode_WhenPressing_ThenOnlyDialogKeysFire()
    {
        var feeder = CreateFeeder();
        feeder.State = feeder.State.WithMode(KeyMode.Confirm);

        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("j").Outcome);
        Assert.Equal(ActionIds.ConfirmYes, feeder.Feed("y").Action);
        Assert.Equal(ActionIds.ConfirmNo, feeder.Feed("n").Action);
    }

    [Fact]
    public void GivenVisualMode_WhenMovingWithCounts_ThenMotionsWorkLikeNormal()
    {
        var feeder = CreateFeeder();
        feeder.State = feeder.State.WithMode(KeyMode.Visual);

        var result = feeder.Feed("3j");

        Assert.Equal(ActionIds.ListDown, result.Action);
        Assert.Equal(3, result.Count);
        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("/").Outcome);
    }

    [Fact]
    public void GivenModeSwitch_WhenChordWasPending_ThenPendingIsDropped()
    {
        var feeder = CreateFeeder();
        feeder.Feed("2g");

        var switched = feeder.State.WithMode(KeyMode.Insert);

        Assert.False(switched.HasPending);
        Assert.Null(switched.Count);
    }

    [Fact]
    public void GivenAPrefixThatIsAlsoABinding_WhenLongerChordsExist_ThenTheLongerChordWins()
    {
        var table = new KeyBindingTable(
        [
            new KeyBinding(KeyMode.Normal, "Global", KeySequenceParser.Parse("g"), new ActionId("short")),
            new KeyBinding(KeyMode.Normal, "Global", KeySequenceParser.Parse("gg"), new ActionId("long")),
        ]);
        var feeder = new KeyFeeder(new KeymapEngine(table), "Global");

        Assert.Equal(KeyOutcome.Pending, feeder.Feed("g").Outcome);
        Assert.Equal(new ActionId("long"), feeder.Feed("g").Action);
        Assert.Single(table.FindConflicts(), conflict => conflict.Kind == BindingConflictKind.PrefixShadow);
    }

    [Fact]
    public void GivenPendingPrefix_WhenAskingForContinuations_ThenAllNextKeysAreListed()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        var feeder = new KeyFeeder(engine);
        feeder.Feed("g");

        var continuations = engine.Continuations(feeder.State, DefaultKeymap.GraphView);

        Assert.Equal(["T", "c", "d", "f", "g", "r", "t"], continuations.Select(continuation => continuation.Key.Name).Order(StringComparer.Ordinal));
        Assert.Equal(ActionIds.ListTop, continuations.Single(continuation => continuation.Key.Name == "g").Action);
        Assert.All(continuations, continuation => Assert.False(continuation.HasMore));
    }

    [Fact]
    public void GivenCtrlWPending_WhenAskingForContinuations_ThenDirectionResizeAndTabKeysAreListed()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        var feeder = new KeyFeeder(engine);
        feeder.Feed("Ctrl-w");

        var names = engine.Continuations(feeder.State, DefaultKeymap.GraphView).Select(continuation => continuation.Key.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["+", "-", "<", "=", ">", "c", "h", "j", "k", "l", "r", "w"], names);
    }
}
