using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Xunit;

namespace Gitunia.Core.Tests.Prompts;

// A picker without a text field (the branch delete list): letters move instead of filtering.
[Trait("Os", "Independent")]
public class ListOnlyPromptTests
{
    [Theory]
    [InlineData("j", 1)]
    [InlineData("l", 1)]
    [InlineData("k", 2)]
    [InlineData("h", 2)]
    [InlineData(KeyNames.Down, 1)]
    public void GivenTheList_WhenAMoveKeyIsPressed_ThenTheHighlightMovesAndWraps(string key, int expected)
    {
        var opened = Open();

        var update = PromptReducer.Reduce(opened, new PromptKeyPressedCommand(new KeyEvent(key)));

        Assert.Equal(expected, update.State.Prompt!.Picker!.Index);
        Assert.Equal(string.Empty, update.State.Prompt.Text);
    }

    [Fact]
    public void GivenTheList_WhenAnotherLetterIsTyped_ThenNothingFiltersAndNothingChanges()
    {
        var opened = Open();

        var update = PromptReducer.Reduce(opened, new PromptKeyPressedCommand(new KeyEvent("x")));

        Assert.Same(opened, update.State);
    }

    [Fact]
    public void GivenTheList_WhenDrawn_ThenThePositionIsInTheContextAndTheHintsNameTheLetters()
    {
        var popup = OpenPromptPopup.Of(Open().Prompt!);

        Assert.Equal("Delete branch", popup.Title);
        Assert.Equal(["Branch to delete at here (1/3)"], popup.Lines);
        Assert.Null(popup.Input);
        Assert.Equal(["> a", "  b", "  c"], popup.Details);
        var hints = popup.Hints.Select(hint => $"{hint.Keys} {hint.Label}");
        Assert.Equal(["Enter choose", "j/k/h/l select", "Esc cancel"], hints);
    }

    private static ShellState Open()
    {
        ImmutableArray<PromptChoice> choices = [new("a", "a"), new("b", "b"), new("c", "c")];
        var request = new PromptRequest(
            "Delete branch",
            "Branch to delete at here",
            "choose",
            new Answer(),
            Choices: choices,
            ListOnly: true);

        return PromptReducer.Reduce(ShellState.Empty, new PromptRequestedCommand(request)).State;
    }

    private sealed record Answer : IPromptResult
    {
        public AppCommand Answered(PromptAnswer answer) => new SetKeyModeCommand(KeyMode.Normal);
    }
}
