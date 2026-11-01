using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Tests.Fixtures;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Prompts;

[Trait("Os", "Independent")]
public class PromptReducerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly ConfirmPrompt AskDelete = new("Delete", ["x"], "y", "Delete");

    [Fact]
    public void GivenARequest_WhenOpening_ThenThePromptIsOpenInInsertModeWithTheStartingText()
    {
        var update = Reduce(ShellState.Empty, Request(initialText: "alpha"));

        var prompt = update.State.Prompt!;
        Assert.Equal("Rename", prompt.Title);
        Assert.Equal("alpha", prompt.Text);
        Assert.Null(prompt.Picker);
        Assert.Equal(KeyMode.Insert, update.State.Mode);
        Assert.Equal(1, update.State.ModeChanges);
        Assert.Empty(update.Effects);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenAnotherIsRequested_ThenTheOpenOneIsKept()
    {
        var opened = Reduce(ShellState.Empty, Request(initialText: "first")).State;

        var update = Reduce(opened, Request(initialText: "second"));

        Assert.Same(opened, update.State);
    }

    [Fact]
    public void GivenAnOpenConfirmDialog_WhenAPromptIsRequested_ThenItReplacesTheDialog()
    {
        var asked = ShellReducer.Reduce(
            ShellState.Empty,
            new ConfirmRequestedCommand(AskDelete, new SetKeyModeCommand(KeyMode.Normal), null)).State;

        var update = Reduce(asked, Request());

        Assert.Null(update.State.Confirm);
        Assert.NotNull(update.State.Prompt);
        Assert.Equal(KeyMode.Insert, update.State.Mode);
    }

    [Fact]
    public void GivenTypedKeys_WhenPressed_ThenTheTextFollowsAndTheCaretStaysAtTheEnd()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "ab")).State;

        state = Press(state, new KeyEvent("c"));
        state = Press(state, new KeyEvent(KeyNames.Backspace));
        state = Press(state, new KeyEvent("d"));

        Assert.Equal("abd", state.Prompt!.Text);
    }

    [Fact]
    public void GivenText_WhenCtrlWAndCtrlUArePressed_ThenTheLastWordAndThenEverythingGo()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "one two")).State;

        state = Press(state, new KeyEvent("w", KeyModifiers.Ctrl));
        Assert.Equal("one ", state.Prompt!.Text);

        state = Press(state, new KeyEvent("u", KeyModifiers.Ctrl));
        Assert.Equal(string.Empty, state.Prompt!.Text);
    }

    [Fact]
    public void GivenAKeyThatDoesNotEdit_WhenPressed_ThenTheStateIsTheSame()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "ab")).State;

        var update = Reduce(state, new PromptKeyPressedCommand(new KeyEvent(KeyNames.Down)));

        Assert.Same(state, update.State);
    }

    [Fact]
    public void GivenNoPrompt_WhenKeysAcceptAndCancelArrive_ThenNothingChanges()
    {
        PromptCommand[] commands =
        [
            new PromptKeyPressedCommand(new KeyEvent("a")),
            new PromptAcceptedCommand(),
            new PromptCancelledCommand(),
        ];

        foreach (var command in commands)
        {
            var update = Reduce(ShellState.Empty, command);

            Assert.Same(ShellState.Empty, update.State);
            Assert.Null(update.Next);
            Assert.Empty(update.Effects);
        }
    }

    [Fact]
    public void GivenTypedText_WhenAccepting_ThenThePromptClosesAndItsResultIsReducedNext()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "x")).State;
        state = Press(state, new KeyEvent("y"));

        var update = Reduce(state, new PromptAcceptedCommand());

        Assert.Null(update.State.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
        var next = Assert.IsType<StatusPostedCommand>(update.Next);
        Assert.Equal("xy|", next.Text);
    }

    [Fact]
    public void GivenAPromptOpenedFromVisualMode_WhenItCloses_ThenVisualModeIsBack()
    {
        var visual = ShellState.Empty.WithMode(KeyMode.Visual);
        var opened = Reduce(visual, Request()).State;

        var accepted = Reduce(opened, new PromptAcceptedCommand());
        var cancelled = Reduce(opened, new PromptCancelledCommand());

        Assert.Equal(KeyMode.Visual, accepted.State.Mode);
        Assert.Equal(KeyMode.Visual, cancelled.State.Mode);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenCancelling_ThenItClosesWithoutAResult()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "x")).State;

        var update = Reduce(state, new PromptCancelledCommand());

        Assert.Null(update.State.Prompt);
        Assert.Equal(KeyMode.Normal, update.State.Mode);
        Assert.Null(update.Next);
    }

    [Fact]
    public void GivenAPromptAndAConfirmDialog_WhenTheDialogIsAnswered_ThenTheModeGoesBackToInsertAndThePromptStays()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "x")).State;
        state = ShellReducer.Reduce(
            state,
            new ConfirmRequestedCommand(AskDelete, new SetKeyModeCommand(KeyMode.Normal), null)).State;
        Assert.Equal(KeyMode.Confirm, state.Mode);

        var answered = ShellReducer.Reduce(state, new ConfirmAnsweredCommand(ConfirmAnswer.No)).State;

        Assert.Equal(KeyMode.Insert, answered.Mode);
        Assert.Equal("x", answered.Prompt!.Text);
    }

    [Fact]
    public void GivenAPicker_WhenOpening_ThenEveryChoiceIsACandidateAndTheFirstIsHighlighted()
    {
        var update = Reduce(ShellState.Empty, Picker("origin", "backup"));

        var picker = update.State.Prompt!.Picker!;
        Assert.Equal(["origin", "backup"], picker.Candidates.Select(choice => choice.Value));
        Assert.Equal(0, picker.Index);
    }

    [Fact]
    public void GivenAPicker_WhenTyping_ThenOnlyMatchingCandidatesRemainAndTheHighlightResets()
    {
        var state = Reduce(ShellState.Empty, Picker("origin", "backup", "bak")).State;
        state = Press(state, new KeyEvent(KeyNames.Down));

        state = Press(state, new KeyEvent("b"));
        state = Press(state, new KeyEvent("a"));

        var picker = state.Prompt!.Picker!;
        Assert.Equal("ba", state.Prompt.Text);
        Assert.Equal(["backup", "bak"], picker.Candidates.Select(choice => choice.Value).Order());
        Assert.Equal(0, picker.Index);
    }

    [Fact]
    public void GivenAPicker_WhenMovingUpFromTheFirst_ThenTheHighlightWrapsToTheLast()
    {
        var state = Reduce(ShellState.Empty, Picker("origin", "backup")).State;

        state = Press(state, new KeyEvent(KeyNames.Up));

        Assert.Equal(1, state.Prompt!.Picker!.Index);
        Assert.Equal(string.Empty, state.Prompt.Text);
    }

    [Fact]
    public void GivenAPicker_WhenMovingWithCtrlJAndTabAndDown_ThenTheHighlightAdvancesEachTime()
    {
        var state = Reduce(ShellState.Empty, Picker("a", "b", "c", "d")).State;

        state = Press(state, new KeyEvent("j", KeyModifiers.Ctrl));
        state = Press(state, new KeyEvent(KeyNames.Tab));
        state = Press(state, new KeyEvent(KeyNames.Down));

        Assert.Equal(3, state.Prompt!.Picker!.Index);
    }

    [Fact]
    public void GivenAHighlightedCandidate_WhenAccepting_ThenTheAnswerCarriesItsValueAndTheTypedFilter()
    {
        var state = Reduce(ShellState.Empty, Picker("origin", "backup")).State;
        state = Press(state, new KeyEvent(KeyNames.Down));

        var update = Reduce(state, new PromptAcceptedCommand());

        var next = Assert.IsType<StatusPostedCommand>(update.Next);
        Assert.Equal("|backup", next.Text);
        Assert.Null(update.State.Prompt);
    }

    [Fact]
    public void GivenNoMatchingCandidate_WhenAccepting_ThenThePromptStaysOpenWithAWarning()
    {
        var state = Reduce(ShellState.Empty, Picker("origin")).State;
        state = Press(state, new KeyEvent("z"));

        var update = Reduce(state, new PromptAcceptedCommand());

        Assert.Same(state, update.State);
        Assert.Null(update.Next);
        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal("No matching remote", post.Text);
        Assert.Equal(StatusSeverity.Warning, post.Severity);
    }

    [Fact]
    public void GivenATextPrompt_WhenDrawn_ThenThePopupShowsTheFieldAndTheAcceptLabel()
    {
        var state = Reduce(ShellState.Empty, Request(initialText: "alpha")).State;

        var popup = OpenPromptPopup.Of(state.Prompt!);

        Assert.Equal("Rename", popup.Title);
        Assert.Equal(["What for"], popup.Lines);
        Assert.Equal("alpha", popup.Input);
        Assert.Equal("Enter save   Esc cancel", popup.Hint);
    }

    [Fact]
    public void GivenAPicker_WhenDrawn_ThenThePopupListsTheCandidatesAndOffersSelectOnlyForSeveral()
    {
        var several = Reduce(ShellState.Empty, Picker("origin", "backup")).State.Prompt!;
        var single = Reduce(ShellState.Empty, Picker("origin")).State.Prompt!;
        var none = Press(Reduce(ShellState.Empty, Picker("origin")).State, new KeyEvent("z")).Prompt!;

        var severalPopup = OpenPromptPopup.Of(several);

        Assert.Equal(["> origin", "  backup", "(1/2)"], severalPopup.Details);
        Assert.Contains("Ctrl-j/k select", severalPopup.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Ctrl-j/k", OpenPromptPopup.Of(single).Hint, StringComparison.Ordinal);
        Assert.Equal(["No matching remote"], OpenPromptPopup.Of(none).Details);
    }

    [Fact]
    public void GivenEveryConcretePromptCommand_WhenReducing_ThenTheReducerHasACaseForIt()
    {
        var commands = ConcreteSubtypes.InstancesOf<PromptCommand>();
        Assert.NotEmpty(commands);

        foreach (var command in commands)
        {
            try
            {
                PromptReducer.Reduce(ShellState.Empty, command);
            }
            catch (UnhandledCommandException)
            {
                Assert.Fail($"{command.GetType().Name} has no case in PromptReducer");
            }
            catch (Exception)
            {
                // The empty command was handled by its case and tripped over its empty fields.
            }
        }
    }

    private static ShellUpdate Reduce(ShellState state, PromptRequest request) =>
        PromptReducer.Reduce(state, new PromptRequestedCommand(request));

    private static ShellUpdate Reduce(ShellState state, PromptCommand command) =>
        PromptReducer.Reduce(state, command);

    private static ShellState Press(ShellState state, KeyEvent key) =>
        PromptReducer.Reduce(state, new PromptKeyPressedCommand(key)).State;

    private static PromptRequest Request(string initialText = "") =>
        new("Rename", "What for", "save", new EchoResult(), initialText);

    private static PromptRequest Picker(params string[] values) =>
        new(
            "Pick",
            "What for",
            "choose",
            new EchoResult(),
            Choices: [.. values.Select(value => new PromptChoice(value, value))],
            NoMatch: "No matching remote",
            Owner: new TabId(1));

    // A result that is easy to read back: the status text is "<typed text>|<chosen value>".
    private sealed record EchoResult : IPromptResult
    {
        public AppCommand Answered(PromptAnswer answer) =>
            new StatusPostedCommand($"{answer.Text}|{answer.Choice}", StatusSeverity.Info, Start);
    }
}
