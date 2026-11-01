using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.Core.Tests.Shell;

[Trait("Os", "Independent")]
public class ShellReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly ConfirmPrompt Prompt = new("Delete", ["Delete branch x?"], "Cannot be undone.", "Delete");

    private static readonly AppCommand Accepted = new RunActionCommand(() => { });

    [Fact]
    public void GivenAModeSwitch_WhenReducing_ThenTheModeIsStoredAndTheSwitchIsCounted()
    {
        var first = ShellReducer.Reduce(ShellState.Empty, new SetKeyModeCommand(KeyMode.Insert));
        var second = ShellReducer.Reduce(first.State, new SetKeyModeCommand(KeyMode.Insert));

        Assert.Equal(KeyMode.Insert, second.State.Mode);
        Assert.Equal(2, second.State.ModeChanges);
    }

    [Fact]
    public void GivenAMessage_WhenPosting_ThenItShowsAndTheBarIsScheduledToAgeAtTheEndOfTheHighlight()
    {
        var update = Reduce(ShellState.Empty, new StatusPostedCommand("Pushed", StatusSeverity.Info, Start));

        Assert.Equal("Pushed", update.State.Status.Current(Start)?.Text);
        var schedule = Assert.IsType<ScheduleStatusAgingEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusLifetime.Fresh, schedule.After);
    }

    [Fact]
    public void GivenTheSameMessageStillShowing_WhenPostingItAgain_ThenTheHistoryHasOneEntry()
    {
        var first = Reduce(ShellState.Empty, new StatusPostedCommand("could not read", StatusSeverity.Error, Start));

        var second = Reduce(first.State, new StatusPostedCommand("could not read", StatusSeverity.Error, Start));

        Assert.Single(second.State.Status.History);
        Assert.Empty(second.Effects);
    }

    [Fact]
    public void GivenAnExpiredMessage_WhenPostingTheSameTextAgain_ThenItShowsAgain()
    {
        var first = Reduce(ShellState.Empty, new StatusPostedCommand("Fetched", StatusSeverity.Info, Start));
        var later = Start + TimeSpan.FromMinutes(1);

        var second = Reduce(first.State, new StatusPostedCommand("Fetched", StatusSeverity.Info, later));

        Assert.Equal(2, second.State.Status.History.Count);
        Assert.NotNull(second.State.Status.Current(later));
    }

    [Fact]
    public void GivenAnInfo_WhenItsLifetimeEnds_ThenItIsGoneAndTheHistoryKeepsIt()
    {
        var update = Reduce(ShellState.Empty, new StatusPostedCommand("Pushed", StatusSeverity.Info, Start));
        var lifetime = StatusLifetime.Of(StatusSeverity.Info);

        Assert.NotNull(update.State.Status.Current(Start + lifetime - TimeSpan.FromMilliseconds(1)));
        Assert.Null(update.State.Status.Current(Start + lifetime));
        Assert.Single(update.State.Status.History);
    }

    [Fact]
    public void GivenAnError_WhenTheInfoLifetimePasses_ThenTheErrorStillShows()
    {
        var update = Reduce(ShellState.Empty, new StatusPostedCommand("push failed", StatusSeverity.Error, Start));

        var shown = update.State.Status.Current(Start + StatusLifetime.Of(StatusSeverity.Info) + TimeSpan.FromSeconds(1));

        Assert.Equal("push failed", shown?.Text);
    }

    [Fact]
    public void GivenAnAgedMessage_WhenTimePasses_ThenTheNextStepIsScheduledUntilItExpires()
    {
        var posted = Reduce(ShellState.Empty, new StatusPostedCommand("done", StatusSeverity.Info, Start)).State;

        var afterHighlight = Reduce(posted, new StatusAgedCommand(Start + StatusLifetime.Fresh));
        var afterExpiry = Reduce(posted, new StatusAgedCommand(Start + StatusLifetime.Of(StatusSeverity.Info)));

        var schedule = Assert.IsType<ScheduleStatusAgingEffect>(Assert.Single(afterHighlight.Effects));
        Assert.Equal(StatusLifetime.Of(StatusSeverity.Info) - StatusLifetime.Fresh, schedule.After);
        Assert.Empty(afterExpiry.Effects);
    }

    [Fact]
    public void GivenAShowingMessage_WhenDismissing_ThenItIsHiddenButKeptInTheHistory()
    {
        var posted = Reduce(ShellState.Empty, new StatusPostedCommand("worktrees failed", StatusSeverity.Error, Start));

        var update = Reduce(posted.State, new StatusDismissedCommand(Start));

        Assert.Null(update.State.Status.Current(Start));
        Assert.Equal("worktrees failed", update.State.Status.History[0].Text);
    }

    [Fact]
    public void GivenNothingShowing_WhenDismissing_ThenTheStateIsTheSame()
    {
        var update = Reduce(ShellState.Empty, new StatusDismissedCommand(Start));

        Assert.Same(ShellState.Empty, update.State);
    }

    [Fact]
    public void GivenAnExpiredMessage_WhenDismissing_ThenTheStateIsTheSame()
    {
        var posted = Reduce(ShellState.Empty, new StatusPostedCommand("done", StatusSeverity.Info, Start)).State;

        var update = Reduce(posted, new StatusDismissedCommand(Start + TimeSpan.FromMinutes(1)));

        Assert.Same(posted, update.State);
    }

    [Fact]
    public void GivenAMessageWithLineBreaks_WhenPosting_ThenItIsOneLine()
    {
        var update = Reduce(ShellState.Empty, new StatusPostedCommand("first\nsecond", StatusSeverity.Warning, Start));

        Assert.Equal("first second", update.State.Status.Current(Start)?.Text);
    }

    [Fact]
    public void GivenANormalMode_WhenAskingToConfirm_ThenTheDialogOpensInConfirmModeAndRemembersTheMode()
    {
        var update = Ask(ShellState.Empty);

        var dialog = update.State.Confirm!;
        Assert.Same(Prompt, dialog.Prompt);
        Assert.Same(Accepted, dialog.OnAccepted);
        Assert.Equal(KeyMode.Normal, dialog.ReturnMode);
        Assert.Equal(KeyMode.Confirm, update.State.Mode);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenYIsAnswered_ThenItClosesTheModeReturnsAndTheAcceptedCommandFollows()
    {
        var asked = Ask(ShellState.Empty with { Mode = KeyMode.Visual }).State;

        var update = ShellReducer.Reduce(asked, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(update.State.Confirm);
        Assert.Equal(KeyMode.Visual, update.State.Mode);
        Assert.Same(Accepted, update.Next);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenNIsAnswered_ThenNothingFollows()
    {
        var asked = Ask(ShellState.Empty).State;

        var update = ShellReducer.Reduce(asked, new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Null(update.State.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
        Assert.Null(update.Next);
    }

    [Fact]
    public void GivenNoDialog_WhenAnAnswerArrives_ThenNothingHappens()
    {
        var update = ShellReducer.Reduce(ShellState.Empty, new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Same(ShellState.Empty, update.State);
        Assert.Null(update.Next);
    }

    [Fact]
    public void GivenADialogOpenOverADialog_WhenAskingAgain_ThenTheFirstReturnModeIsKept()
    {
        var first = Ask(ShellState.Empty with { Mode = KeyMode.Visual }).State;

        var second = Ask(first).State;

        Assert.Equal(KeyMode.Visual, second.Confirm!.ReturnMode);
    }

    [Fact]
    public void GivenAScreenThatIsTooSmall_WhenAskingToConfirm_ThenNothingOpensAndTheRefusalFollows()
    {
        var small = ShellState.Empty with { Screen = new ScreenExtent(80, 6) };
        var refusal = new StatusPostedCommand("The screen is too small to ask", StatusSeverity.Warning, Start);

        var update = ShellReducer.Reduce(small, new ConfirmRequestedCommand(Prompt, Accepted, refusal));

        Assert.Null(update.State.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
        Assert.Same(refusal, update.Next);
    }

    [Fact]
    public void GivenAScreenThatIsTooNarrow_WhenAskingToConfirm_ThenNothingOpens()
    {
        var narrow = ShellState.Empty with { Screen = new ScreenExtent(10, 40) };

        var update = ShellReducer.Reduce(narrow, new ConfirmRequestedCommand(Prompt, Accepted, null));

        Assert.Null(update.State.Confirm);
        Assert.Null(update.Next);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenTheScreenBecomesTooSmall_ThenItClosesWithoutAnsweringAndWarns()
    {
        var asked = Ask(ShellState.Empty with { Screen = new ScreenExtent(100, 40) }).State;

        var update = ShellReducer.Reduce(asked, new ScreenResizedCommand(new ScreenExtent(100, 6), Start));

        Assert.Null(update.State.Confirm);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
        var warning = Assert.IsType<StatusPostedCommand>(update.Next);
        Assert.Equal(ShellReducer.ResizedNotice, warning.Text);
        Assert.Equal(StatusSeverity.Warning, warning.Severity);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenTheScreenStillFits_ThenItStaysOpen()
    {
        var asked = Ask(ShellState.Empty).State;

        var update = ShellReducer.Reduce(asked, new ScreenResizedCommand(new ScreenExtent(100, 40), Start));

        Assert.Same(asked.Confirm, update.State.Confirm);
        Assert.Null(update.Next);
    }

    [Fact]
    public void GivenOutput_WhenShowing_ThenTheBoxOpensInNormalModeAndRemembersTheMode()
    {
        var shown = new OutputShownCommand("Commit failed", "Hook output", ["line 1", "line 2"]);

        var update = ShellReducer.Reduce(ShellState.Empty with { Mode = KeyMode.Insert }, shown);

        Assert.Equal("Commit failed", update.State.Output!.Title);
        Assert.Equal(["line 1", "line 2"], update.State.Output.Lines);
        Assert.Equal(KeyMode.Insert, update.State.Output.ReturnMode);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
    }

    [Fact]
    public void GivenOutput_WhenClosing_ThenTheModeIsRestored()
    {
        var shown = ShellReducer.Reduce(
            ShellState.Empty with { Mode = KeyMode.Insert },
            new OutputShownCommand("t", "h", ["x"])).State;

        var update = ShellReducer.Reduce(shown, new OutputClosedCommand());

        Assert.Null(update.State.Output);
        Assert.Equal(KeyMode.Insert, update.State.Mode);
    }

    [Fact]
    public void GivenAnOpenDialog_WhenOutputIsShown_ThenTheOutputReplacesItAndKeepsTheReturnMode()
    {
        var asked = Ask(ShellState.Empty with { Mode = KeyMode.Visual }).State;

        var update = ShellReducer.Reduce(asked, new OutputShownCommand("t", "h", ["x"]));

        Assert.Null(update.State.Confirm);
        Assert.Equal(KeyMode.Visual, update.State.Output!.ReturnMode);
    }

    [Fact]
    public void GivenAnOpenOutput_WhenAskingToConfirm_ThenTheDialogReplacesIt()
    {
        var shown = ShellReducer.Reduce(ShellState.Empty, new OutputShownCommand("t", "h", ["x"])).State;

        var update = Ask(shown);

        Assert.Null(update.State.Output);
        Assert.NotNull(update.State.Confirm);
    }

    [Fact]
    public void GivenACallback_WhenReducingTheBridgeCommand_ThenItComesOutAsAnEffect()
    {
        var run = new RunActionCommand(() => { });

        var update = ShellReducer.Reduce(ShellState.Empty, run);

        Assert.Same(run.Run, Assert.IsType<RunActionEffect>(Assert.Single(update.Effects)).Run);
    }

    private static ShellUpdate Reduce(ShellState state, ShellCommand command) => ShellReducer.Reduce(state, command);

    private static ShellUpdate Ask(ShellState state) =>
        ShellReducer.Reduce(state, new ConfirmRequestedCommand(Prompt, Accepted, null));
}
