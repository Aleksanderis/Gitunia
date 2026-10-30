using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Tabs;
using System.Globalization;
using System.Text;
using Gitunia.Git.Conflicts;
using Gitunia.Git.Conflicts.Models;
using Xunit;

namespace Gitunia.Core.Tests.Conflicts;

[Trait("Os", "Independent")]
public class ConflictEditorStateTests
{
    // Result lines: 0 top, 1-5 the first conflict, 6 mid, 7-11 the second conflict, 12 end.
    private const string Text =
        "top\n<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n";

    private const string Diff3Text =
        "top\n<<<<<<< HEAD\nA1\n||||||| base\nO1\n=======\nB1\n>>>>>>> other\nend\n";

    private const string LongHunk =
        "top\n<<<<<<< HEAD\na\nb\nc\n=======\nx\ny\n>>>>>>> other\nend\n";

    [Fact]
    public void GivenAFreshFile_WhenCreatingTheState_ThenNothingIsResolvedAndSavingKeepsTheFileAsItWas()
    {
        var state = State(Text);

        Assert.Equal(2, state.Hunks.Count);
        Assert.Equal(2, state.UnresolvedCount);
        Assert.False(state.IsModified);
        Assert.False(state.IsComplete);
        Assert.Equal(Text, state.ToText());
        Assert.Equal(1, state.Row);
        Assert.Equal(ConflictPane.Ours, state.ActivePane);
    }

    [Theory]
    [InlineData(ConflictPick.Ours, "top\nA1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n")]
    [InlineData(ConflictPick.Theirs, "top\nB1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n")]
    [InlineData(ConflictPick.Both, "top\nA1\nB1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n")]
    public void GivenTheFirstConflict_WhenTakingAVersion_ThenOnlyItsRegionIsReplaced(
        ConflictPick pick,
        string expected)
    {
        var state = State(Text).Take(pick);

        Assert.Equal(expected, state.ToText());
        Assert.Equal(pick, state.SelectedPick);
        Assert.Equal(1, state.ResolvedCount);
        Assert.True(state.IsModified);
    }

    [Fact]
    public void GivenAPickThatChangesTheLineCount_WhenPickingTheNextConflict_ThenItsRegionMovedWithTheText()
    {
        var state = State(Text).Take(ConflictPick.Both).NextConflict().Take(ConflictPick.Theirs);

        Assert.Equal("top\nA1\nB1\nmid\nB2\nend\n", state.ToText());
        Assert.True(state.IsComplete);
        Assert.Equal(new ConflictRegion(1, 2, ConflictPick.Both), state.Result.Regions[0]);
        Assert.Equal(new ConflictRegion(4, 1, ConflictPick.Theirs), state.Result.Regions[1]);
    }

    [Fact]
    public void GivenAPick_WhenUndoingAndRedoing_ThenTheFileFollowsAndTheCursorGoesToItsConflict()
    {
        var picked = State(Text).Take(ConflictPick.Ours).NextConflict().Take(ConflictPick.Theirs);
        var pickedText = picked.ToText();

        var undone = picked.Undo();
        Assert.Equal("top\nA1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n", undone.ToText());
        Assert.Equal(1, undone.Index);
        var back = undone.Undo();
        Assert.Equal(Text, back.ToText());
        Assert.False(back.IsModified);
        Assert.Equal("top\nA1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n", back.Redo().ToText());
        Assert.Equal(pickedText, back.Redo().Redo().ToText());
    }

    [Fact]
    public void GivenTheSamePickTwice_WhenTakingItAgain_ThenNoUndoStepIsAdded()
    {
        var once = State(Text).Take(ConflictPick.Ours);

        var twice = once.Take(ConflictPick.Ours);

        Assert.Single(twice.UndoStack!);
        Assert.Equal(once.ToText(), twice.ToText());
    }

    [Fact]
    public void GivenAPick_WhenResetting_ThenTheMarkersComeBackAndUndoRestoresThePick()
    {
        var picked = State(Text).Take(ConflictPick.Theirs);
        var pickedText = picked.ToText();

        var reset = picked.Reset();

        Assert.Equal(Text, reset.ToText());
        Assert.Equal(ConflictPick.None, reset.SelectedPick);
        Assert.Equal(pickedText, reset.Undo().ToText());
    }

    [Fact]
    public void GivenAnEarlierPick_WhenPickingAllUnresolved_ThenTheEarlierPickStaysAndOneUndoRestoresBoth()
    {
        var earlier = State(Text).Take(ConflictPick.Both);
        var earlierText = earlier.ToText();

        var state = earlier.PickAllUnresolved(ConflictPick.Theirs);

        Assert.Equal("top\nA1\nB1\nmid\nB2\nend\n", state.ToText());
        Assert.True(state.IsComplete);
        Assert.Equal(earlierText, state.Undo().ToText());
    }

    [Fact]
    public void GivenNothingOpenOrABothPick_WhenPickingAllUnresolved_ThenNothingChanges()
    {
        var done = State(Text).PickAllUnresolved(ConflictPick.Ours);

        Assert.Same(done, done.PickAllUnresolved(ConflictPick.Theirs));
        Assert.Same(done, done.PickAllUnresolved(ConflictPick.Ours));
        var open = State(Text);
        Assert.Same(open, open.PickAllUnresolved(ConflictPick.Both));
    }

    [Fact]
    public void GivenADiff3Conflict_WhenTakingTheBase_ThenTheAncestorLinesAreTheResult()
    {
        var state = State(Diff3Text).Take(ConflictPick.Base);

        Assert.Equal("top\nO1\nend\n", state.ToText());
        Assert.Equal(ConflictPick.Base, state.SelectedPick);
    }

    [Fact]
    public void GivenAConflictWithoutABase_WhenTakingTheBase_ThenItIsRefused()
    {
        var state = State(Text);

        Assert.False(state.CanTake(ConflictPick.Base));
        Assert.Same(state, state.Take(ConflictPick.Base));
    }

    [Fact]
    public void GivenAnEmptyBase_WhenTakingTheBase_ThenTheConflictResolvesToNothing()
    {
        var parts = ConflictParser.Parse(Text).Parts!
            .Select(WithEmptyBase)
            .ToList();
        var state = ConflictEditorState.Create(new TabId(1), "file.txt", parts).Take(ConflictPick.Base);

        Assert.Equal("top\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n", state.ToText());
        Assert.Equal(0, state.Result.Regions[0].Length);
        Assert.Equal(1, state.ResolvedCount);
    }

    [Fact]
    public void GivenAVisualSelectionInOurs_WhenTakingIt_ThenOnlyThoseLinesReplaceTheMarkers()
    {
        var state = State(LongHunk).ToggleVisual().MoveCursor(1);

        var taken = state.TakeSelectedLines();

        Assert.Equal("top\na\nb\nend\n", taken.ToText());
        Assert.Equal(ConflictPick.Lines, taken.SelectedPick);
        Assert.False(taken.IsVisual);
        Assert.True(taken.IsComplete);
    }

    [Fact]
    public void GivenLinesAlreadyTaken_WhenSelectingLinesInTheirs_ThenTheyAreAppended()
    {
        var first = State(LongHunk).ToggleVisual().MoveCursor(1).TakeSelectedLines();

        var second = first.FocusPane(ConflictPane.Theirs).ToggleVisual().TakeSelectedLines();

        Assert.Equal("top\na\nb\ny\nend\n", second.ToText());
        Assert.Equal(new ConflictRegion(1, 3, ConflictPick.Lines), second.Result.Regions[0]);
        Assert.Equal("top\na\nb\nend\n", second.Undo().ToText());
    }

    [Fact]
    public void GivenASelectionOnFillerRowsOrOutsideTheConflict_WhenTakingIt_ThenNothingIsCopied()
    {
        var onFiller = State(LongHunk).FocusPane(ConflictPane.Theirs).MoveCursor(2).ToggleVisual();
        Assert.Empty(onFiller.SelectedLines());
        var taken = onFiller.TakeSelectedLines();
        Assert.Equal(LongHunk, taken.ToText());
        Assert.False(taken.IsModified);
        Assert.False(taken.IsVisual);

        var outside = State(LongHunk).MoveToTop().ToggleVisual();
        Assert.Empty(outside.SelectedLines());
    }

    [Fact]
    public void GivenASelectionPastTheConflict_WhenTakingIt_ThenOnlyTheConflictsLinesAreCopied()
    {
        var state = State(LongHunk).ToggleVisual().MoveCursor(10);

        Assert.Equal(["a", "b", "c"], state.SelectedLines());
    }

    [Fact]
    public void GivenTheResultPane_WhenTypingIntoAConflict_ThenTheRegionIsEditedAndCountsAsResolvedOnlyWithoutMarkers()
    {
        var state = State(Text).BeginEdit();
        Assert.Equal(ConflictPane.Result, state.ActivePane);
        Assert.True(state.IsEditing);

        state = state.TypeKey(new KeyEvent("X"));

        Assert.Equal(ConflictPick.Edited, state.SelectedPick);
        Assert.StartsWith(
            "top\nX<<<<<<< HEAD\nA1\n=======\nB1\n>>>>>>> other\nmid",
            state.ToText(),
            StringComparison.Ordinal);
        Assert.Equal(2, state.UnresolvedCount);
    }

    [Fact]
    public void GivenTheMarkerLinesDeletedByHand_WhenCounting_ThenTheConflictIsResolved()
    {
        var state = State(Text).FocusPane(ConflictPane.Result);
        Assert.Equal(1, state.Result.Editor.CursorRow);

        state = state.ApplyResultCommand(ConflictResultCommand.DeleteLine);
        state = state.MoveCursor(1).ApplyResultCommand(ConflictResultCommand.DeleteLine);
        Assert.Equal(2, state.UnresolvedCount);
        state = state.MoveCursor(1).ApplyResultCommand(ConflictResultCommand.DeleteLine);

        Assert.Equal("top\nA1\nB1\nmid\n<<<<<<< HEAD\nA2\n=======\nB2\n>>>>>>> other\nend\n", state.ToText());
        Assert.Equal(1, state.UnresolvedCount);
        Assert.Equal(new ConflictRegion(1, 2, ConflictPick.Edited), state.Result.Regions[0]);
        Assert.Equal(7 - 3, state.Result.Regions[1].Start);
    }

    [Fact]
    public void GivenATypingSession_WhenEndingIt_ThenItIsOneUndoStep()
    {
        var typed = State(Text).BeginEdit();
        foreach (var character in "abc")
        {
            typed = typed.TypeKey(new KeyEvent(character.ToString()));
        }

        typed = typed.EndEdit();

        Assert.False(typed.IsEditing);
        Assert.Single(typed.UndoStack!);
        Assert.Contains("abc<<<<<<<", typed.ToText(), StringComparison.Ordinal);
        Assert.Equal(Text, typed.Undo().ToText());
    }

    [Fact]
    public void GivenATypingSessionThatChangedNothing_WhenEndingIt_ThenNoUndoStepIsLeft()
    {
        var state = State(Text).BeginEdit().EndEdit();

        Assert.False(state.CanUndo);
        Assert.False(state.IsModified);
    }

    [Fact]
    public void GivenTypedTextInAConflict_WhenTakingAVersionAgain_ThenThePickReplacesItAndUndoBringsItBack()
    {
        var edited = State(Text).Take(ConflictPick.Ours).BeginEdit().TypeKey(new KeyEvent("Z")).EndEdit();
        Assert.Contains("ZA1\nmid", edited.ToText(), StringComparison.Ordinal);

        var picked = edited.FocusPane(ConflictPane.Theirs).Take(ConflictPick.Theirs);

        Assert.Contains("top\nB1\nmid", picked.ToText(), StringComparison.Ordinal);
        Assert.Contains("top\nZA1\nmid", picked.Undo().ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTextTypedOutsideEveryConflict_WhenPickingConflicts_ThenTheEditSurvivesAndTheRegionsStayAligned()
    {
        var typed = State(Text).FocusPane(ConflictPane.Result).MoveToTop().BeginEdit();
        typed = typed.TypeKey(new KeyEvent("Enter")).TypeKey(new KeyEvent("N")).EndEdit();
        Assert.Equal("\nNtop\n<<<<<<<", typed.ToText()[..13]);
        Assert.Equal(new ConflictRegion(2, 5, ConflictPick.None), typed.Result.Regions[0]);
        Assert.Equal(ConflictPick.None, typed.SelectedPick);

        var picked = typed.FocusPane(ConflictPane.Ours)
            .NextConflict().Take(ConflictPick.Ours)
            .NextConflict().Take(ConflictPick.Theirs);

        Assert.Equal("\nNtop\nA1\nmid\nB2\nend\n", picked.ToText());
        Assert.True(picked.IsComplete);
    }

    [Fact]
    public void GivenALineOpenedJustAfterAConflict_WhenPickingIt_ThenTheNewLineStaysOutsideTheRegion()
    {
        var state = State(Text).FocusPane(ConflictPane.Result);
        state = state.MoveCursor(4);
        Assert.Equal(">>>>>>> other", state.Result.Editor.Lines[5]);

        state = state.ApplyResultCommand(ConflictResultCommand.OpenLine).TypeKey(new KeyEvent("n")).EndEdit();

        Assert.Equal(new ConflictRegion(1, 5, ConflictPick.None), state.Result.Regions[0]);
        Assert.Equal(new ConflictRegion(8, 5, ConflictPick.None), state.Result.Regions[1]);
        var picked = state.Take(ConflictPick.Ours);
        Assert.StartsWith("top\nA1\nn\nmid\n", picked.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACommonLineEqualToTheRegionsFirstLine_WhenDeletingIt_ThenTheRegionStillHoldsTheWholePick()
    {
        var state = State("a\n<<<<<<< HEAD\na\nx\n=======\ny\n>>>>>>> other\nend\n").Take(ConflictPick.Ours);
        Assert.Equal("a\na\nx\nend\n", state.ToText());

        state = state.FocusPane(ConflictPane.Result).MoveToTop().ApplyResultCommand(ConflictResultCommand.DeleteLine);

        Assert.Equal(new ConflictRegion(0, 2, ConflictPick.Ours), state.Result.Regions[0]);
        Assert.Equal("y\nend\n", state.Take(ConflictPick.Theirs).ToText());
    }

    [Fact]
    public void GivenARegionWithASeparatorLineAlone_WhenEditedByHand_ThenItCountsAsResolved()
    {
        var state = State("t\n<<<<<<< HEAD\nTitle\n=======\nB\n>>>>>>> other\nend\n").Take(ConflictPick.Ours);

        var edited = state.BeginEdit().TypeKey(new KeyEvent("Enter")).TypeKey(new KeyEvent("=")).EndEdit();

        Assert.True(edited.IsComplete);
    }

    [Fact]
    public void GivenMotionsAndNoOpChanges_WhenRunningResultCommands_ThenNoUndoStepIsAdded()
    {
        var state = State(Text).FocusPane(ConflictPane.Result);

        state = state.ApplyResultCommand(ConflictResultCommand.WordNext)
            .ApplyResultCommand(ConflictResultCommand.WordPrevious)
            .ApplyResultCommand(ConflictResultCommand.Right)
            .ApplyResultCommand(ConflictResultCommand.Left)
            .ApplyResultCommand(ConflictResultCommand.LineStart)
            .ApplyResultCommand(ConflictResultCommand.LineEnd)
            .ApplyResultCommand(ConflictResultCommand.DeleteChar);

        Assert.False(state.IsModified);
    }

    [Fact]
    public void GivenTheResultPane_WhenDeletingACharacterAndALine_ThenEachIsOneUndoStep()
    {
        var state = State(Text).FocusPane(ConflictPane.Result).MoveToTop()
            .ApplyResultCommand(ConflictResultCommand.DeleteChar);
        state = state.ApplyResultCommand(ConflictResultCommand.DeleteLine);

        Assert.Equal(2, state.UndoStack!.Count);
        Assert.Equal("op\n", state.Undo().ToText()[..3]);
        Assert.Equal(Text, state.Undo().Undo().ToText());
    }

    [Fact]
    public void GivenTheVersionPane_WhenRunningResultCommandsOrTyping_ThenNothingChanges()
    {
        var state = State(Text);

        Assert.Same(state, state.ApplyResultCommand(ConflictResultCommand.DeleteLine));
        Assert.Same(state, state.TypeKey(new KeyEvent("x")));
        Assert.Same(state, state.EndEdit());
    }

    [Fact]
    public void GivenAnOpenEdit_WhenMovingPickingOrUndoing_ThenNothingChanges()
    {
        var state = State(Text).Take(ConflictPick.Ours).BeginEdit();

        Assert.Same(state, state.NextConflict());
        Assert.Same(state, state.MoveCursor(1));
        Assert.Same(state, state.Take(ConflictPick.Theirs));
        Assert.Same(state, state.Undo());
        Assert.Same(state, state.Reset());
        Assert.Same(state, state.BeginEdit());
        Assert.Same(state, state.FocusPane(ConflictPane.Ours));
    }

    [Fact]
    public void GivenTwoConflicts_WhenSteppingThroughThem_ThenTheCursorWrapsAndStopsAtTheFileEnds()
    {
        var state = State(Text);

        var second = state.NextConflict();
        Assert.Equal(1, second.Index);
        Assert.Equal(3, second.Row);
        var wrapped = second.NextConflict();
        Assert.Equal(0, wrapped.Index);
        Assert.Equal(1, wrapped.Row);
        Assert.Equal(1, wrapped.PreviousConflict().Index);
        Assert.Equal(0, state.MoveToTop().Row);
        Assert.Equal(0, state.MoveToTop().MoveCursor(-5).Row);
        Assert.Equal(4, state.MoveToBottom().MoveCursor(5).Row);
    }

    [Fact]
    public void GivenTheCursorOnCommonText_WhenMoving_ThenTheCurrentConflictIsTheLastOneVisited()
    {
        var state = State(Text).MoveCursor(1);

        Assert.Equal(2, state.Row);
        Assert.Equal(0, state.Index);
        Assert.Equal(1, state.MoveCursor(1).Index);
    }

    [Fact]
    public void GivenTheResultPane_WhenSteppingThroughTheConflicts_ThenTheEditorCursorLandsOnTheirRegions()
    {
        var state = State(Text).FocusPane(ConflictPane.Result);

        var second = state.NextConflict();

        Assert.Equal(7, second.Result.Editor.CursorRow);
        Assert.Equal(1, second.Index);
        Assert.Equal(0, second.PreviousConflict().Index);
        Assert.Equal(1, second.PreviousConflict().Result.Editor.CursorRow);
    }

    [Fact]
    public void GivenPanes_WhenMovingBetweenThem_ThenTheCursorKeepsItsAlignedPlace()
    {
        var state = State(Text).MoveCursor(2);
        Assert.Equal(3, state.Row);

        var result = state.PaneDown();
        Assert.Equal(ConflictPane.Result, result.ActivePane);
        Assert.Equal(7, result.Result.Editor.CursorRow);
        var up = result.MoveCursor(1).PaneUp();
        Assert.Equal(ConflictPane.Ours, up.ActivePane);
        Assert.Equal(3, up.Row);
        Assert.Equal(ConflictPane.Base, state.PaneLeft().ActivePane);
        Assert.Equal(ConflictPane.Base, state.PaneLeft().PaneLeft().ActivePane);
        Assert.Equal(ConflictPane.Theirs, state.PaneRight().PaneRight().ActivePane);
        Assert.Equal(ConflictPane.Result, state.NextPane().NextPane().ActivePane);
        Assert.Equal(ConflictPane.Base, state.NextPane().NextPane().NextPane().ActivePane);
        Assert.Equal(ConflictPane.Result, state.NextPane().NextPane().PreviousPane().NextPane().ActivePane);
        Assert.Same(result, result.PaneDown());
    }

    [Fact]
    public void GivenAPaneSwitchDuringAVisualSelection_WhenSwitching_ThenTheSelectionEnds()
    {
        var state = State(LongHunk).ToggleVisual().MoveCursor(1);

        Assert.True(state.IsVisual);
        Assert.False(state.PaneRight().IsVisual);
        Assert.False(state.ToggleVisual().IsVisual);
        Assert.False(state.PaneDown().ToggleVisual().IsVisual);
    }

    [Fact]
    public void GivenALongFile_WhenFollowing_ThenTheCursorStaysVisibleInBothPanesAtTheSameHeight()
    {
        var state = State(ManyConflicts(40)).MoveToBottom();

        var followed = state.Follow(8, 10);

        Assert.True(followed.TopScroll <= followed.Row && followed.Row < followed.TopScroll + 8);
        var line = followed.ResultLineOfRow(followed.Row);
        Assert.True(followed.ResultScroll <= line && line < followed.ResultScroll + 10);
        Assert.Equal(followed.Row - followed.TopScroll, line - followed.ResultScroll);
        Assert.Same(followed, followed.Follow(8, 10));
        var inResult = followed.PaneDown().MoveCursor(-3).Follow(8, 10);
        Assert.True(inResult.ResultScroll <= inResult.Result.Editor.CursorRow);
        Assert.True(inResult.TopScroll <= inResult.Row && inResult.Row < inResult.TopScroll + 8);
    }

    [Fact]
    public void GivenAJumpToAConflict_WhenFollowingCentered_ThenTheConflictSitsAThirdDownBothPanes()
    {
        var state = State(ManyConflicts(40)).NextConflict().NextConflict().NextConflict();

        var centered = state.Follow(12, 15, isCentered: true);

        Assert.Equal(state.Row - 4, centered.TopScroll);
        Assert.Equal(centered.ResultLineOfRow(state.Row) - 5, centered.ResultScroll);
        var moved = centered.MoveCursor(1).Follow(12, 15);
        Assert.Equal(centered.TopScroll, moved.TopScroll);
        Assert.Equal(centered.ResultScroll, moved.ResultScroll);
    }

    [Fact]
    public void GivenAThousandLinesAndSixtyConflicts_WhenPickingEveryOneAndUndoingAll_ThenItIsExact()
    {
        var text = ManyConflicts(60);
        var state = State(text);
        Assert.Equal(60, state.UnresolvedCount);

        for (var step = 0; step < 60; step++)
        {
            state = state.Take(step % 2 == 0 ? ConflictPick.Ours : ConflictPick.Theirs).NextConflict();
        }

        Assert.True(state.IsComplete);
        Assert.DoesNotContain("<<<<<<<", state.ToText(), StringComparison.Ordinal);
        Assert.Contains("ours0\n", state.ToText(), StringComparison.Ordinal);
        Assert.Contains("theirs1\n", state.ToText(), StringComparison.Ordinal);
        for (var step = 0; step < 60; step++)
        {
            state = state.Undo();
        }

        Assert.Equal(text, state.ToText());
        Assert.False(state.IsModified);
    }

    [Fact]
    public void GivenACrlfFileWithoutAFinalLineBreak_WhenSaving_ThenBothAreKept()
    {
        var crlf = "top\r\n<<<<<<< HEAD\r\nA\r\n=======\r\nB\r\n>>>>>>> other";

        var state = State(crlf);
        Assert.Equal(crlf, state.ToText());
        var picked = state.Take(ConflictPick.Both);

        Assert.Equal("top\r\nA\r\nB", picked.ToText());
        Assert.False(picked.Format.HadMixedLineEndings);
    }

    [Fact]
    public void GivenAFileMixingLineEndings_WhenOpening_ThenItIsFlaggedAndWrittenInTheCommonStyle()
    {
        var mixed = "top\r\nmid\r\n<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> other\nend\r\n";

        var state = State(mixed);

        Assert.True(state.Format.HadMixedLineEndings);
        Assert.Equal("\n", state.Format.LineEnding);
        Assert.Equal(mixed.Replace("\r\n", "\n", StringComparison.Ordinal), state.ToText());
    }

    [Fact]
    public void GivenAnEmptiedResult_WhenSaving_ThenTheFileIsEmpty()
    {
        var emptied = ConflictEditorState.Create(
            new TabId(1),
            "file.txt",
            ConflictParser.Parse("<<<<<<< HEAD\nA\n=======\nB\n>>>>>>> other\n").Parts!);
        var all = emptied.FocusPane(ConflictPane.Result);
        for (var step = 0; step < 5; step++)
        {
            all = all.ApplyResultCommand(ConflictResultCommand.DeleteLine);
        }

        Assert.Equal(string.Empty, all.ToText());
    }

    [Fact]
    public void GivenAChangedFile_WhenAskingToDiscard_ThenTheNextMoveClearsTheQuestion()
    {
        var state = State(Text).Take(ConflictPick.Ours).AskDiscard();

        Assert.True(state.IsDiscardPending);
        Assert.False(state.MoveCursor(1).IsDiscardPending);
    }

    private static ConflictPart WithEmptyBase(ConflictPart part) =>
        part.Hunk is { } hunk ? ConflictPart.OfHunk(hunk with { Base = string.Empty }) : part;

    private static ConflictEditorState State(string text) =>
        ConflictEditorState.Create(new TabId(1), "file.txt", ConflictParser.Parse(text).Parts!);

    // A file of about 16 lines per conflict: common lines, then a conflict of ours<n> and theirs<n>.
    private static string ManyConflicts(int count)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < count; index++)
        {
            for (var line = 0; line < 12; line++)
            {
                builder.Append(CultureInfo.InvariantCulture, $"common {index} {line}\n");
            }

            builder.Append(CultureInfo.InvariantCulture, $"<<<<<<< HEAD\nours{index}\n");
            builder.Append(CultureInfo.InvariantCulture, $"=======\ntheirs{index}\n>>>>>>> other\n");
        }

        return builder.ToString();
    }
}
