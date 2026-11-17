using Gitunia.App;
using Gitunia.Core;
using Gitunia.Core.Keymap;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Prompts;

// The actions a click or the wheel on the prompt popup runs: the footer hints (accept, cancel, move), a row and the wheel.
public sealed class PromptInputHandlerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly Dispatcher _host = new TestShell().Host;
    private readonly PromptInputHandler _handler;

    public PromptInputHandlerTests() => _handler = new PromptInputHandler(_host);

    [Fact]
    public void GivenNoPrompt_WhenAnActionOfTheFooterArrives_ThenItIsNotTaken()
    {
        Assert.False(_handler.TryExecute(ActionIds.ListDown, null));
        Assert.False(_handler.TryExecute(ActionIds.PromptRow, 1));
    }

    [Fact]
    public void GivenAFilterPrompt_WhenTheWheelAndTheMoveHintRun_ThenTheHighlightMovesByTheCountAndStopsAtTheEnds()
    {
        Open(Picker("origin", "backup", "bak"));

        Assert.True(_handler.TryExecute(ActionIds.ListDown, 1));
        Assert.Equal(1, _host.State.Shell.Prompt!.Picker!.Index);
        Assert.True(_handler.TryExecute(ActionIds.ListDown, 5));
        Assert.Equal(2, _host.State.Shell.Prompt!.Picker!.Index);
        Assert.True(_handler.TryExecute(ActionIds.ListUp, null));
        Assert.Equal(1, _host.State.Shell.Prompt!.Picker!.Index);
    }

    [Fact]
    public void GivenATextPrompt_WhenTheWheelRuns_ThenItIsTakenSoNoPaneBehindMovesAndNothingChanges()
    {
        Open(new PromptRequest("Rename", "What for", "save", new Echo(), "abc"));

        Assert.True(_handler.TryExecute(ActionIds.ListDown, 3));

        Assert.Equal("abc", _host.State.Shell.Prompt!.Text);
    }

    [Fact]
    public void GivenAFilterPrompt_WhenARowRelativeToTheHighlightIsClicked_ThenItTakesTheHighlightAndEnterStillAccepts()
    {
        Open(Picker("origin", "backup", "bak"));

        Assert.True(_handler.TryExecute(ActionIds.PromptRow, 2));
        Assert.Equal(2, _host.State.Shell.Prompt!.Picker!.Index);
        Assert.True(_handler.TryExecute(ActionIds.PromptRow, -1));

        Assert.Equal(1, _host.State.Shell.Prompt!.Picker!.Index);
        Assert.NotNull(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenAOneKeyPrompt_WhenAnOptionRowIsClicked_ThenThePromptClosesWithThatOption()
    {
        Open(new PromptRequest(
            "Reset",
            "Mode",
            string.Empty,
            new Echo(),
            KeyChoices: [new PromptKeyChoice("s", "soft", "soft"), new PromptKeyChoice("h", "hard", "hard")]));

        Assert.True(_handler.TryExecute(ActionIds.PromptRow, 1));

        Assert.Null(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenAPrompt_WhenTheFooterHintsRun_ThenAcceptClosesAndCancelClosesWithoutAnAnswer()
    {
        Open(new PromptRequest("Rename", "What for", "save", new Echo(), "abc"));
        Assert.True(_handler.TryExecute(ActionIds.Cancel, null));
        Assert.Null(_host.State.Shell.Prompt);

        Open(new PromptRequest("Rename", "What for", "save", new Echo(), "abc"));
        Assert.True(_handler.TryExecute(ActionIds.InputAccept, null));
        Assert.Null(_host.State.Shell.Prompt);
    }

    [Fact]
    public void GivenEveryFooterHintOfEveryPopup_WhenTheirActionsAreRun_ThenTheHandlerTakesThem()
    {
        PromptRequest[] requests =
        [
            new("Rename", "What for", "save", new Echo(), "abc"),
            Picker("origin", "backup"),
            new("Reset", "Mode", string.Empty, new Echo(), KeyChoices: [new PromptKeyChoice("s", "soft", "soft")]),
            new("Pick", "What for", "choose", new Echo(), Choices: [new PromptChoice("a", "a")], ListOnly: true),
        ];
        foreach (var request in requests)
        {
            Open(request);
            var popup = OpenPromptPopup.Of(_host.State.Shell.Prompt!);

            foreach (var action in popup.Hints.SelectMany(hint => hint.Actions.IsDefault ? [] : hint.Actions)
                .Where(action => action != ActionIds.Cancel))
            {
                Assert.True(_handler.TryExecute(action, null), $"{popup.Title}: {action} was not taken.");
                if (_host.State.Shell.Prompt is null)
                {
                    Open(request);
                }
            }

            _handler.TryExecute(ActionIds.Cancel, null);
        }
    }

    private void Open(PromptRequest request) => _host.Dispatch(new PromptRequestedCommand(request));

    private static PromptRequest Picker(params string[] values) =>
        new(
            "Pick",
            "What for",
            "choose",
            new Echo(),
            Choices: [.. values.Select(value => new PromptChoice(value, value))],
            NoMatch: "No matching remote",
            Owner: new TabId(1));

    private sealed record Echo : IPromptResult
    {
        public AppCommand Answered(PromptAnswer answer) =>
            new StatusPostedCommand($"{answer.Text}|{answer.Choice}", StatusSeverity.Info, Start);
    }
}
