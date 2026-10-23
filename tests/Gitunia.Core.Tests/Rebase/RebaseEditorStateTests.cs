using Gitunia.Core.Rebase;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Rebase;
using Xunit;

namespace Gitunia.Core.Tests.Rebase;

public class RebaseEditorStateTests
{
    [Fact]
    public void GivenAFreshRange_WhenCreatingTheState_ThenEveryCommitIsAPickInReplayOrderAndNothingIsEdited()
    {
        var state = State(3);

        Assert.Equal(["c0", "c1", "c2"], state.Rows.Select(row => row.Hash));
        Assert.All(state.Rows, row => Assert.Equal(RebaseTodoAction.Pick, row.Action));
        Assert.False(state.IsModified);
        Assert.Null(state.Problem);
        Assert.Equal(3, state.KeptCount);
    }

    [Fact]
    public void GivenARow_WhenMovingItDown_ThenTheCommitsSwapAndTheHighlightFollowsTheCommit()
    {
        var state = State(3).MoveRow(1);

        Assert.Equal(["c1", "c0", "c2"], state.Rows.Select(row => row.Hash));
        Assert.Equal("c0", state.Selected!.Hash);
        Assert.True(state.IsModified);
    }

    [Fact]
    public void GivenTheEnds_WhenMovingPastThem_ThenNothingMoves()
    {
        var state = State(3);

        Assert.Equal(state.Rows, state.MoveRow(-1).Rows);
        Assert.Equal(state.Rows, state.MoveTo(2).MoveRow(1).Rows);
        Assert.Equal(2, state.MoveTo(2).MoveRow(1).SelectedIndex);
    }

    [Fact]
    public void GivenACount_WhenMovingFurtherThanTheEnd_ThenTheCommitStopsAtTheEndWithoutSwappingTheOthers()
    {
        var state = State(4).MoveRow(9);

        Assert.Equal(["c1", "c2", "c3", "c0"], state.Rows.Select(row => row.Hash));
        Assert.Equal(["c0", "c3", "c1", "c2"], State(4).MoveTo(3).MoveRow(-2).Rows.Select(row => row.Hash));
    }

    [Fact]
    public void GivenAMoveThereAndBack_WhenComparingWithTheOriginal_ThenTheTodoIsUnmodifiedAgain()
    {
        var state = State(3).MoveRow(1).MoveRow(-1);

        Assert.False(state.IsModified);
    }

    [Fact]
    public void GivenAnEmptyTodo_WhenMovingAndSettingActions_ThenNothingHappensAndNothingThrows()
    {
        var state = State(0);

        Assert.Same(state, state.MoveRow(1));
        Assert.Same(state, state.SetAction(RebaseTodoAction.Drop));
        Assert.Null(state.Selected);
        Assert.Empty(state.ToEntries());
    }

    [Fact]
    public void GivenAnAction_WhenSettingSquashFixupAndDrop_ThenOnlyTheHighlightedCommitChanges()
    {
        var state = State(4)
            .MoveTo(1).SetAction(RebaseTodoAction.Squash)
            .MoveTo(2).SetAction(RebaseTodoAction.Fixup)
            .MoveTo(3).SetAction(RebaseTodoAction.Drop);

        Assert.Equal(
            [RebaseTodoAction.Pick, RebaseTodoAction.Squash, RebaseTodoAction.Fixup, RebaseTodoAction.Drop],
            state.Rows.Select(row => row.Action));
        Assert.Equal(3, state.KeptCount);
        Assert.Equal(1, state.DroppedCount);
    }

    [Fact]
    public void GivenReword_WhenSettingItWithoutAMessage_ThenItIsIgnored()
    {
        var state = State(2);

        Assert.Same(state, state.SetAction(RebaseTodoAction.Reword));
    }

    [Fact]
    public void GivenAFirstKeptCommitThatMelds_WhenAskingForTheProblem_ThenItNamesTheRuleAndDropsAreSkipped()
    {
        var state = State(3).SetAction(RebaseTodoAction.Drop).MoveTo(1).SetAction(RebaseTodoAction.Squash);

        Assert.Contains("cannot be a squash or a fixup", state.Problem, StringComparison.Ordinal);
        var fixedState = state.MoveTo(1).SetAction(RebaseTodoAction.Pick);
        Assert.Null(fixedState.Problem);
    }

    [Fact]
    public void GivenEveryCommitDropped_WhenAskingForTheProblem_ThenThereIsNoneBecauseGitTakesAnEmptyResult()
    {
        var state = State(2).SetAction(RebaseTodoAction.Drop).MoveTo(1).SetAction(RebaseTodoAction.Drop);

        Assert.Null(state.Problem);
        Assert.Equal(0, state.KeptCount);
    }

    [Fact]
    public void GivenARewordEdit_WhenSavingATypedMessage_ThenTheRowIsARewordWithTheMessageAndTheEditorCloses()
    {
        var state = State(2).MoveTo(1).BeginEdit("old subject\n\nbody");
        state.Edit!.Editor.SetText("new subject\n\nnew body\n\n");

        var saved = state.SaveEdit();

        Assert.Null(saved.Edit);
        Assert.Equal(RebaseTodoAction.Reword, saved.Rows[1].Action);
        Assert.Equal("new subject\n\nnew body", saved.Rows[1].Message);
        Assert.Equal("new subject", saved.Rows[1].ShownSubject);
        var expected = new RebaseTodoEntry("c1", RebaseTodoAction.Reword, "new subject\n\nnew body");
        Assert.Equal(expected, saved.ToEntries()[1]);
    }

    [Fact]
    public void GivenABlankMessage_WhenSaving_ThenTheEditorStaysOpenAndNothingChanges()
    {
        var state = State(2).BeginEdit("subject");
        state.Edit!.Editor.SetText("  \n ");

        var saved = state.SaveEdit();

        Assert.Same(state, saved);
        Assert.False(saved.IsModified);
    }

    [Fact]
    public void GivenAnEdit_WhenCancelling_ThenThePreviousActionAndMessageStay()
    {
        var state = State(2).BeginEdit("subject");
        state.Edit!.Editor.SetText("changed");

        var cancelled = state.CancelEdit();

        Assert.Null(cancelled.Edit);
        Assert.Equal(RebaseTodoAction.Pick, cancelled.Rows[0].Action);
        Assert.Null(cancelled.Rows[0].Message);
    }

    [Fact]
    public void GivenARewordMovedAway_WhenChoosingPickAndRewordAgain_ThenOnlyAnActiveRewordSendsItsMessage()
    {
        var reworded = State(2).BeginEdit("a");
        reworded.Edit!.Editor.SetText("kept text");
        var state = reworded.SaveEdit().SetAction(RebaseTodoAction.Pick);

        Assert.Null(state.ToEntries()[0].Message);
        Assert.False(state.IsModified);
        Assert.Equal("kept text", state.Rows[0].Message);
        Assert.Equal("c0 subject", state.Rows[0].ShownSubject);
    }

    [Fact]
    public void GivenAnEditedTodo_WhenAskingToDiscard_ThenAnyFurtherEditClearsTheQuestion()
    {
        var state = State(3).MoveRow(1).AskDiscard();

        Assert.True(state.IsDiscardPending);
        Assert.False(state.MoveTo(0).IsDiscardPending);
        Assert.False(state.SetAction(RebaseTodoAction.Drop).IsDiscardPending);
    }

    [Fact]
    public void GivenTheEntries_WhenConverting_ThenEveryCommitAppearsOnceWithItsActionInOrder()
    {
        var state = State(3).MoveRow(2).SetAction(RebaseTodoAction.Fixup);

        var entries = state.ToEntries();

        Assert.Equal(["c1", "c2", "c0"], entries.Select(entry => entry.Commit));
        Assert.Equal(RebaseTodoAction.Fixup, entries[2].Action);
        RebaseTodoValidator.Validate(
            [new("c0", "s"), new("c1", "s"), new("c2", "s")],
            entries);
    }

    [Fact]
    public void GivenMergeStructureRows_WhenChangingActions_ThenOnlyPlainCommitsChangeAndCountsSkipStructure()
    {
        var state = MergeState();

        Assert.Equal(2, state.CommitCount);
        Assert.Equal(2, state.KeptCount);
        Assert.Same(state.Rows, state.MoveTo(0).SetAction(RebaseTodoAction.Drop).Rows);
        Assert.Same(state.Rows, state.MoveTo(3).SetAction(RebaseTodoAction.Drop).Rows);
        Assert.Same(state.Rows, state.MoveTo(3).BeginEdit("text").Rows);
        Assert.Equal(RebaseTodoAction.Drop, state.MoveTo(1).SetAction(RebaseTodoAction.Drop).Rows[1].Action);
        Assert.Equal(1, state.MoveTo(1).SetAction(RebaseTodoAction.Drop).DroppedCount);
    }

    [Fact]
    public void GivenMergeRows_WhenConvertingToEntries_ThenLabelsResetsAndMergeArgumentsSurvive()
    {
        var entries = MergeState().ToEntries();

        Assert.Equal(
            [RebaseTodoAction.Label, RebaseTodoAction.Pick, RebaseTodoAction.Label, RebaseTodoAction.Merge],
            entries.Select(entry => entry.Action));
        Assert.Equal("side # Merge", entries[^1].Argument);
        Assert.Equal("onto", entries[0].Commit);
    }

    [Fact]
    public void GivenAMergeMovedBeforeItsLabel_WhenReadingTheProblem_ThenItIsReported()
    {
        var state = MergeState().MoveTo(3).MoveRow(-1);

        Assert.Contains("side", state.Problem, StringComparison.Ordinal);
        Assert.Null(MergeState().Problem);
    }

    private static RebaseEditorState MergeState() =>
        RebaseEditorState.Create(
            new TabId(1),
            "topic",
            "main",
            RebaseBase.OntoCommit("base").WithMerges(),
            "tip",
            [
                new RebaseTodoCommit("onto", string.Empty, RebaseTodoAction.Label),
                new RebaseTodoCommit("aaa", "A"),
                new RebaseTodoCommit("side", string.Empty, RebaseTodoAction.Label),
                new RebaseTodoCommit("mmm", "Merge", RebaseTodoAction.Merge, "side # Merge"),
            ]);

    private static RebaseEditorState State(int count) =>
        RebaseEditorState.Create(
            new TabId(1),
            "topic",
            "main",
            RebaseBase.OntoCommit("base"),
            "tip",
            [.. Enumerable.Range(0, count).Select(index => new RebaseTodoCommit($"c{index}", $"c{index} subject"))]);
}
