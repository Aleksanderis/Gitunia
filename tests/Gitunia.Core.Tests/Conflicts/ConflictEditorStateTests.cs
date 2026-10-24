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
    public void GivenAClosingMarkerOnTheLastLineWithoutNewline_WhenSaving_ThenAPickEndsWithItsOwnLineEnd()
    {
        const string text = "a\n<<<<<<< x\nours\n=======\ntheirs\n>>>>>>> y";

        var state = State(text);

        Assert.Equal(text, state.ToText());
        Assert.Equal("a\nours\n", state.Pick(ConflictPick.Ours).ToText());
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

    [Fact]
    public void GivenSeveralChanges_WhenUndoingAndRedoing_ThenEachStepIsTakenBackAndBroughtBackInOrder()
    {
        var state = State(Text).Pick(ConflictPick.Ours).Next().Pick(ConflictPick.Theirs).Pick(ConflictPick.Both);

        var undone = state.Undo();
        Assert.Equal(ConflictPick.Theirs, undone.Picks[1]);
        undone = undone.Undo();
        Assert.Equal(ConflictPick.None, undone.Picks[1]);
        Assert.Equal(ConflictPick.Ours, undone.Picks[0]);
        undone = undone.Undo();
        Assert.Equal(Text, undone.ToText());
        Assert.False(undone.CanUndo);
        Assert.Same(undone, undone.Undo());

        var redone = undone.Redo().Redo().Redo();
        Assert.Equal(state.Picks, redone.Picks);
        Assert.Equal(state.ToText(), redone.ToText());
        Assert.False(redone.CanRedo);
        Assert.Same(redone, redone.Redo());
    }

    [Fact]
    public void GivenAnUndo_WhenMakingANewChange_ThenRedoIsGone()
    {
        var state = State(Text).Pick(ConflictPick.Ours).Undo();
        Assert.True(state.CanRedo);

        var changed = state.Pick(ConflictPick.Theirs);

        Assert.False(changed.CanRedo);
        Assert.Single(changed.UndoStack!);
    }

    [Fact]
    public void GivenAChangeOnTheFirstHunk_WhenUndoingFromTheSecond_ThenTheCursorGoesToThatHunkAndRedoComesBack()
    {
        var state = State(Text).Pick(ConflictPick.Ours).Next();

        var undone = state.Undo();
        Assert.Equal(0, undone.Index);
        var redone = undone.Next().Redo();

        Assert.Equal(0, redone.Index);
        Assert.Equal(ConflictPick.Ours, redone.Picks[0]);
    }

    [Fact]
    public void GivenMoreChangesThanTheLimit_WhenUndoing_ThenOnlyTheLatestAreKept()
    {
        var state = State(Text);
        for (var step = 0; step < ConflictEditorState.UndoLimit + 10; step++)
        {
            state = state.Pick(step % 2 == 0 ? ConflictPick.Ours : ConflictPick.Theirs);
        }

        Assert.Equal(ConflictEditorState.UndoLimit, state.UndoStack!.Count);
    }

    [Fact]
    public void GivenAnEdit_WhenAccepted_ThenTheTypedTextIsTheResultAndOneUndoRemovesIt()
    {
        var state = State(Text).Pick(ConflictPick.Ours).BeginEdit();
        Assert.True(state.IsEditing);
        state.Edit!.Editor.SetText("custom one\ncustom two");

        var accepted = state.AcceptEdit();

        Assert.False(accepted.IsEditing);
        Assert.Equal(ConflictPick.Edited, accepted.Picks[0]);
        Assert.Equal(
            "top\ncustom one\ncustom two\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n",
            accepted.ToText());
        var undone = accepted.Undo();
        Assert.Equal(ConflictPick.Ours, undone.Picks[0]);
        Assert.Contains("A1\nmid", undone.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnEditOfAnUnresolvedHunk_WhenStarted_ThenItBeginsWithBothSidesAndAnUnchangedAcceptResolvesNothing()
    {
        var state = State(Text).BeginEdit();

        Assert.Equal("A1\nB1\n", state.Edit!.Editor.Text);
        var accepted = state.AcceptEdit();

        Assert.Equal(ConflictPick.None, accepted.Picks[0]);
        Assert.False(accepted.CanUndo);
        Assert.Equal(Text, accepted.ToText());
    }

    [Fact]
    public void GivenAnEditEmptiedCompletely_WhenAccepted_ThenTheHunkResolvesToNothing()
    {
        var state = State(Text).Pick(ConflictPick.Theirs).BeginEdit();
        state.Edit!.Editor.Clear();

        var accepted = state.AcceptEdit();

        Assert.Equal(ConflictPick.Edited, accepted.Picks[0]);
        Assert.Equal("top\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n", accepted.ToText());
    }

    [Fact]
    public void GivenACrlfFile_WhenAcceptingAnEdit_ThenTheTextGetsCrlfLineBreaks()
    {
        var crlf = Text.Replace("\n", "\r\n", StringComparison.Ordinal);
        var state = State(crlf).Pick(ConflictPick.Ours).BeginEdit();
        state.Edit!.Editor.SetText("one\ntwo");

        var saved = state.AcceptEdit().ToText();

        Assert.Contains("one\r\ntwo\r\nmid\r\n", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("one\ntwo", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOpenEdit_WhenMovingPickingOrUndoing_ThenNothingChanges()
    {
        var state = State(Text).Pick(ConflictPick.Ours).BeginEdit();

        Assert.Equal(0, state.Next().Index);
        Assert.Same(state, state.Pick(ConflictPick.Theirs));
        Assert.Same(state, state.Undo());
        Assert.Same(state, state.BeginEdit());
    }

    [Fact]
    public void GivenEditedText_WhenPickingASideAfterwards_ThenThePickReplacesItAndUndoBringsTheTextBack()
    {
        var edited = State(Text).Pick(ConflictPick.Ours).BeginEdit();
        edited.Edit!.Editor.SetText("typed\n");
        var accepted = edited.AcceptEdit();

        var picked = accepted.Pick(ConflictPick.Theirs);

        Assert.Contains("B1\nmid", picked.ToText(), StringComparison.Ordinal);
        Assert.Contains("typed\nmid", picked.Undo().ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheBaseToggle_WhenToggledTwice_ThenItIsOffAgainAndNoUndoStepWasAdded()
    {
        var state = State(Text).ToggleBase();

        Assert.True(state.IsBaseShown);
        Assert.False(state.ToggleBase().IsBaseShown);
        Assert.False(state.CanUndo);
    }

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);
}
