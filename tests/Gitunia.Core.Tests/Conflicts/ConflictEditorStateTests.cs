using Gitunia.Core.Conflicts;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Conflicts;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictEditorStateTests
{
    private const string Text =
        "top\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n";

    [Fact]
    public void GivenAFreshFile_WhenCreatingTheState_ThenNothingIsResolvedAndSavingKeepsTheFileAsItWas()
    {
        var state = State(Text);

        Assert.Equal(2, state.Hunks.Count);
        Assert.Equal(2, state.UnresolvedCount);
        Assert.False(state.IsModified);
        Assert.False(state.IsComplete);
        Assert.Equal(Text, state.ToText());
    }

    [Theory]
    [InlineData(ConflictPick.Ours, "A1\n")]
    [InlineData(ConflictPick.Theirs, "B1\n")]
    [InlineData(ConflictPick.Both, "A1\nB1\n")]
    public void GivenAPick_WhenSaving_ThenOnlyThatHunkIsReplacedAndTheOtherKeepsItsMarkers(
        ConflictPick pick,
        string expected)
    {
        var state = State(Text).Pick(pick);

        var saved = state.ToText();

        Assert.Equal(
            $"top\n{expected}mid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n",
            saved);
        Assert.Equal(1, state.ResolvedCount);
        Assert.True(state.IsModified);
    }

    [Fact]
    public void GivenAPick_WhenUndoingIt_ThenTheHunkIsUnresolvedAgain()
    {
        var state = State(Text).Pick(ConflictPick.Ours).Pick(ConflictPick.None);

        Assert.Equal(Text, state.ToText());
        Assert.False(state.IsModified);
    }

    [Fact]
    public void GivenEveryHunkPicked_WhenAsking_ThenTheFileIsCompleteWithNoMarkersLeft()
    {
        var state = State(Text).Pick(ConflictPick.Ours).Next().Pick(ConflictPick.Theirs);

        Assert.True(state.IsComplete);
        Assert.Equal("top\nA1\nmid\nB2\nend\n", state.ToText());
    }

    [Fact]
    public void GivenTheEnds_WhenStepping_ThenTheCursorWraps()
    {
        var state = State(Text);

        Assert.Equal(1, state.Next().Index);
        Assert.Equal(0, state.Next().Next().Index);
        Assert.Equal(1, state.Previous().Index);
    }

    [Fact]
    public void GivenPicksAreChanged_WhenMoving_ThenAPendingDiscardIsForgotten()
    {
        var state = State(Text).Pick(ConflictPick.Ours).AskDiscard();

        Assert.True(state.IsDiscardPending);
        Assert.False(state.Next().IsDiscardPending);
        Assert.False(state.Pick(ConflictPick.Theirs).IsDiscardPending);
    }

    [Fact]
    public void GivenCrLfText_WhenPickingBoth_ThenTheTerminatorsStayCrLf()
    {
        var state = State("<<<<<<< a\r\nx\r\n=======\r\ny\r\n>>>>>>> b\r\n").Pick(ConflictPick.Both);

        Assert.Equal("x\r\ny\r\n", state.ToText());
    }

    [Fact]
    public void GivenAHunk_WhenAskingForContext_ThenTheLinesNextToItComeBackAndNoneAcrossAnotherHunk()
    {
        var state = State("1\n2\n3\n<<<<<<< a\nx\n=======\ny\n>>>>>>> b\n<<<<<<< a\nz\n=======\nw\n>>>>>>> b\n4\n");

        Assert.Equal(["2", "3"], state.ContextAround(0, before: true, count: 2));
        Assert.Empty(state.ContextAround(0, before: false, count: 2));
        Assert.Empty(state.ContextAround(1, before: true, count: 2));
        Assert.Equal(["4"], state.ContextAround(1, before: false, count: 2));
    }

    [Fact]
    public void GivenAFileWithNoHunks_WhenPicking_ThenNothingChanges()
    {
        var state = State("plain\n");

        Assert.Same(state, state.Pick(ConflictPick.Ours));
        Assert.Null(state.Selected);
        Assert.Equal(0, state.Next().Index);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("a\n", 1)]
    [InlineData("a\r\nb", 2)]
    [InlineData("a\n\n", 2)]
    public void GivenText_WhenSplittingIntoLines_ThenATrailingTerminatorAddsNoLine(string text, int expected)
    {
        Assert.Equal(expected, ConflictEditorState.Lines(text).Count);
    }

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
