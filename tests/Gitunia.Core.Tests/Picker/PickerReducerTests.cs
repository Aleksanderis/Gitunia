using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class PickerReducerTests
{
    private static PickerContext Context() => PickerFixtures.Context(
        discovered:
        [
            PickerFixtures.PathOf("code", "alpha"),
            PickerFixtures.PathOf("code", "beta"),
            PickerFixtures.PathOf("code", "gamma"),
        ],
        directories: new Dictionary<string, string[]> { [PickerFixtures.PathOf("code")] = ["alpha", "beta"] });

    private static PickerState Type(PickerState state, string text) => PickerReducer.Reduce(state, new TypeTextCommand(text), Context());

    [Fact]
    public void GivenOpenPicker_WhenTyping_ThenRowsAreFilteredAndHighlightResets()
    {
        var state = PickerReducer.Open(Context());
        state = PickerReducer.Reduce(state, new MoveHighlightCommand(2), Context());

        state = Type(state, "be");

        Assert.Equal("be", state.Query);
        Assert.Equal(["beta"], state.Rows.Select(row => row.Name));
        Assert.Equal(0, state.Highlight);
    }

    [Fact]
    public void GivenTypedText_WhenBackspacing_ThenTheLastCharacterIsRemoved()
    {
        var state = Type(PickerReducer.Open(Context()), "alx");

        state = PickerReducer.Reduce(state, new BackspaceCommand(), Context());

        Assert.Equal("al", state.Query);
        Assert.Equal(["alpha"], state.Rows.Select(row => row.Name));
    }

    [Fact]
    public void GivenSurrogatePairAtTheEnd_WhenBackspacing_ThenTheWholeCharacterIsRemoved()
    {
        var state = Type(PickerReducer.Open(Context()), "a\U0001F600");

        state = PickerReducer.Reduce(state, new BackspaceCommand(), Context());

        Assert.Equal("a", state.Query);
    }

    [Fact]
    public void GivenFirstRow_WhenMovingUp_ThenTheHighlightStays()
    {
        var state = PickerReducer.Open(Context());

        state = PickerReducer.Reduce(state, new MoveHighlightCommand(-1), Context());

        Assert.Equal(0, state.Highlight);
    }

    [Fact]
    public void GivenLastRow_WhenMovingDown_ThenTheHighlightStays()
    {
        var state = PickerReducer.Open(Context());

        state = PickerReducer.Reduce(state, new MoveHighlightCommand(10), Context());

        Assert.Equal(2, state.Highlight);
        Assert.Equal("gamma", state.HighlightedRow!.Name);
    }

    [Fact]
    public void GivenPathQuery_WhenPressingTab_ThenThePathIsCompleted()
    {
        var state = Type(PickerReducer.Open(Context()), "~/code/b");

        state = PickerReducer.Reduce(state, new CompletePathCommand(), Context());

        Assert.Equal("~/code/beta/", state.Query);
    }

    [Fact]
    public void GivenFuzzyQuery_WhenPressingTab_ThenNothingChanges()
    {
        var state = Type(PickerReducer.Open(Context()), "be");

        var completed = PickerReducer.Reduce(state, new CompletePathCommand(), Context());

        Assert.Equal(state, completed);
    }

    [Fact]
    public void GivenMessage_WhenTypingAgain_ThenTheMessageIsCleared()
    {
        var state = PickerReducer.Reduce(PickerReducer.Open(Context()), new SetMessageCommand("not a repository"), Context());

        state = Type(state, "a");

        Assert.Equal(string.Empty, state.Message);
    }

    [Fact]
    public void GivenHighlightedRepository_WhenRefreshingAfterAPin_ThenTheSameRepositoryStaysHighlighted()
    {
        var state = PickerReducer.Open(Context());
        state = PickerReducer.Reduce(state, new MoveHighlightCommand(2), Context());
        var pinned = WorkspaceReducer.Reduce(Workspace.Empty, new TogglePinCommand(PickerFixtures.PathOf("code", "gamma"))).Workspace;
        var context = PickerFixtures.Context(pinned, discovered: [.. Context().Discovered]);

        state = PickerReducer.Reduce(state, new RefreshRowsCommand(), context);

        Assert.Equal("gamma", state.HighlightedRow!.Name);
        Assert.Equal(0, state.Highlight);
        Assert.True(state.HighlightedRow.IsPinned);
    }

    [Fact]
    public void GivenLocatingTab_WhenOpening_ThenTheTabIdIsRemembered()
    {
        var state = PickerReducer.Open(Context(), new TabId(7), "~/code/");

        Assert.Equal(new TabId(7), state.LocatingTabId);
        Assert.Equal("~/code/", state.Query);
        Assert.Equal(["alpha", "beta"], state.Rows.Select(row => row.Name));
    }
}
