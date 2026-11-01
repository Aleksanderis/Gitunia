using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Commits;

// The message box of the Changes view and the commit, through the root reducer: keys in, state and effects out.
public class CommitReducerTests
{
    [Fact]
    public void GivenTheLists_WhenWritingTheMessage_ThenTheBoxHasFocusAndInsertModeIsOn()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeWriteMessage);

        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.IsEditing);
    }

    [Fact]
    public void GivenInsertMode_WhenTypingKeysAndNewLines_ThenTheDraftHoldsTheMessage()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Type("Subject");
        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Type("Body ünï");

        Assert.Equal("Subject\n\nBody ünï", harness.Draft.Editor.Text);
        Assert.Single(CommitRules.SessionDrafts(harness.State.Workspace));
    }

    [Fact]
    public void GivenNoInsertMode_WhenAKeyIsTyped_ThenTheBoxDoesNotTakeIt()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("kept");

        harness.Send(new CommitKeyTyped(harness.TabId, new KeyEvent("x")));

        Assert.Equal("kept", harness.Draft.Editor.Text);
        Assert.False(harness.IsEditing);
    }

    [Fact]
    public void GivenAnotherPromptInInsertMode_WhenEnterArrives_ThenTheBoxDoesNotAddALine()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("one");
        harness.Run(ActionIds.Cancel);
        harness.Send(new SetKeyModeCommand(KeyMode.Insert));

        harness.Run(ActionIds.WorkingTreeMessageNewLine);

        Assert.Equal("one", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnEmptyMessage_WhenCommitting_ThenItIsRefusedWithoutRunningGitAndTheBoxGetsTheFocus()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal(CommitCheck.EmptyMessage, harness.LastStatus);
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
        Assert.True(harness.IsEditing);
    }

    [Fact]
    public void GivenNothingStagedAndNoUnstaged_WhenCommitting_ThenItIsRefusedUnlessAMergeIsInProgress()
    {
        var harness = new CommitHarness();
        harness.SetText("Message");

        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal(CommitCheck.NothingStaged, harness.LastStatus);
        Assert.Empty(harness.Effects.OfType<ShowConfirmEffect>());
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());

        harness.SetOperation(RepositoryOperation.Merge);
        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Single(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenAMessageAndOptions_WhenCommitting_ThenTheEffectCarriesThemAndTheTabRunsUntilTheResult()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Fix the thing");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        harness.Run(ActionIds.WorkingTreeToggleNoVerify);

        harness.Run(ActionIds.WorkingTreeCommit);

        var effect = harness.Take<RunCommitEffect>();
        Assert.Equal(new CommitRequest("Fix the thing", true, true, false), effect.Request);
        Assert.Equal(harness.Tab.Location, effect.Location);
        Assert.True(harness.Tab.Running.IsRunning(RunningOperationKind.Commit));
    }

    [Fact]
    public void GivenACommitSucceeded_WhenTheResultArrives_ThenTheDraftIsClearedFocusReturnsAndTheStatusIsReadAgain()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Fix the thing");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        harness.Run(ActionIds.WorkingTreeCommit);
        var request = harness.Take<RunCommitEffect>().Request;

        harness.Send(new CommitSucceeded(harness.TabId, request, new CommitResult("2222222222", null), null));

        Assert.True(harness.Draft.Editor.IsBlank);
        Assert.True(harness.Draft.SignOff);
        Assert.Equal("Committed 2222222", harness.LastStatus);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Tab.Commit.IsInserting);
        Assert.False(harness.Tab.Running.IsBusy);
        Assert.Equal(harness.TabId, harness.Take<RefreshTabEffect>().TabId);
    }

    [Fact]
    public void GivenAFailedCommit_WhenTheResultArrives_ThenTheDraftStaysAndTheOutputOpens()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        var lines = CommitFailureReport.Lines("lint failed: a.txt\nfix it\n");

        harness.Send(new CommitFailed(harness.TabId, "Commit failed: lint failed: a.txt", lines));

        Assert.Equal("Message", harness.Draft.Editor.Text);
        Assert.Equal("Commit failed: lint failed: a.txt", harness.LastStatus);
        Assert.Equal(StatusSeverity.Error, harness.Effects.OfType<PostStatusEffect>().Last().Severity);
        var output = harness.State.Shell.Output!;
        Assert.Equal(CommitFailureReport.Title, output.Title);
        Assert.Equal(CommitFailureReport.Heading, output.Heading);
        Assert.Contains("lint failed: a.txt", output.Lines);
        Assert.False(harness.Tab.Running.IsBusy);
        Assert.Single(harness.Effects.OfType<RefreshTabEffect>());
    }

    [Fact]
    public void GivenACommitThatFailsOnABackgroundTab_WhenTheTabIsShown_ThenItsOutputOpensOnce()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var first = harness.TabId;
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        harness.OpenOtherTab();

        harness.Send(new CommitFailed(first, "Commit failed: lint failed: a.txt", ["lint failed: a.txt"]));

        Assert.Null(harness.State.Shell.Output);
        Assert.Equal("Commit failed: lint failed: a.txt", harness.LastStatus);

        harness.Activate(first);

        Assert.Equal(["lint failed: a.txt"], harness.State.Shell.Output!.Lines);
        Assert.Null(harness.Tab.Commit.PendingFailure);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    public void GivenARunningCommit_WhenEscapeIsPressed_ThenItIsCancelledAndTheDraftStays(string escapeAction)
    {
        // Esc is Cancel in Insert mode and NavigateBack in the box's Normal mode: both stop the commit.
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);

        harness.Run(ActionIds.WorkingTreeCommit);
        Assert.Equal(CommitTexts.RunningNotice, harness.LastStatus);
        harness.Run(new ActionId(escapeAction));

        Assert.Equal(harness.TabId, harness.Take<CancelCommitEffect>().TabId);
        Assert.Equal(CommitTexts.CancellingNotice, harness.LastStatus);
        harness.Send(new CommitFailed(harness.TabId, CommitTexts.CancelledNotice, null));
        Assert.Equal("Message", harness.Draft.Editor.Text);
        Assert.StartsWith("Commit cancelled", harness.LastStatus, StringComparison.Ordinal);
        Assert.Null(harness.State.Shell.Output);
        Assert.False(harness.Tab.Running.IsBusy);
        Assert.Single(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenARunningCommit_WhenTheTabIsClosedAndTheResultArrives_ThenItIsDropped()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var tabId = harness.TabId;
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        harness.Send(new CloseTabCommand(tabId));
        var before = harness.State;

        var update = harness.Send(new CommitFailed(tabId, CommitTexts.CancelledNotice, null));

        Assert.Same(before, update.State);
        Assert.Empty(update.Effects);
        Assert.Empty(CommitRules.SessionDrafts(harness.State.Workspace));
    }

    [Fact]
    public void GivenARunningCommit_WhenEnterIsPressedInTheBox_ThenTheMessageIsNotEdited()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Message");
        harness.Run(ActionIds.WorkingTreeCommit);

        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Send(new CommitKeyTyped(harness.TabId, new KeyEvent("x")));

        Assert.Equal("Message", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenInsertMode_WhenEscapeIsPressed_ThenNormalModeStartsAndTheBoxKeepsFocus()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Run(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenUsingMotionsAndDeletes_ThenTheTextChangesAndTheBoxKeepsFocus()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("one two");
        harness.Run(ActionIds.Cancel);

        harness.Run(ActionIds.WorkingTreeMessageLineStart);
        harness.Run(ActionIds.WorkingTreeMessageRight);
        harness.Run(ActionIds.WorkingTreeMessageDeleteChar);
        harness.Run(ActionIds.WorkingTreeMessageWordNext);
        harness.Run(ActionIds.WorkingTreeMessageLeft);

        Assert.Equal("oe two", harness.Draft.Editor.Text);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
        Assert.True(CommitRules.ShowsCursor(harness.State));
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenAppendingAndOpeningALine_ThenInsertModeStartsAtThePlaceVimWould()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("ab");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeMessageLineStart);

        harness.Run(ActionIds.WorkingTreeMessageAppend);
        harness.Type("X");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeMessageOpenLine);
        harness.Type("Y");

        Assert.Equal("aXb\nY", harness.Draft.Editor.Text);
        Assert.Equal(KeyMode.Insert, harness.Mode);
    }

    [Fact]
    public void GivenAnInsertSession_WhenUndoingInNormalMode_ThenTheWholeSessionIsUndone()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeEditMessage);
        harness.Type(" more");
        harness.Run(ActionIds.Cancel);

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal("first", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenNothingToUndo_WhenUndoing_ThenTheStatusSaysSo()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingMessage)));

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal(CommitTexts.NothingToUndoNotice, harness.LastStatus);
    }

    [Fact]
    public void GivenAnEarlierChange_WhenInsertEndsWithNothingTyped_ThenUndoRevertsTheEarlierChange()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeEditMessage);
        harness.Run(ActionIds.Cancel);

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal(string.Empty, harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenNormalKeysRunWithTheFocusElsewhere_WhenTheBoxIsNotFocused_ThenTheTextIsUntouched()
    {
        var harness = new CommitHarness(Modified("a.txt"), StagedModified("b.txt"));
        harness.SetText("keep");

        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);

        Assert.Equal("keep", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnotherPromptOwnsInsertMode_WhenTheBoxIsFocused_ThenTheBoxShowsNoCursor()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingMessage)));

        harness.Send(new SetKeyModeCommand(KeyMode.Insert));

        Assert.False(CommitRules.ShowsCursor(harness.State));
        Assert.False(harness.IsEditing);
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenGoingBack_ThenTheListWithTheSelectionHasFocus()
    {
        var harness = new CommitHarness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Send(new TabCommand(harness.TabId, new SelectWorkingTreeFileCommand(WorkingTreeSection.Staged, 0)));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Run(ActionIds.Cancel);

        Assert.True(harness.Run(ActionIds.NavigateBack));

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheListsFocused_WhenGoingBackOrCancelling_ThenTheBoxLeavesTheActionToTheOthers()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        Assert.False(harness.Run(ActionIds.NavigateBack));
        Assert.False(harness.Run(ActionIds.Cancel));
    }

    [Fact]
    public void GivenAnEarlierChange_WhenInsertEndsByAFocusMoveWithNothingTyped_ThenUndoRevertsTheEarlierChange()
    {
        var harness = new CommitHarness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeEditMessage);
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));
        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingMessage)));

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal(string.Empty, harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnEarlierChange_WhenDeleteCharAndDeleteLineDoNothing_ThenOneUndoRevertsTheEarlierChange()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);
        harness.Run(ActionIds.WorkingTreeMessageDeleteChar);
        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal("first", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenInsertModeInTheBox_WhenFocusMovesAway_ThenInsertModeEnds()
    {
        var harness = new CommitHarness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Send(new TabCommand(harness.TabId, new FocusPaneCommand(PaneKind.WorkingUnstaged)));

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.IsEditing);
        Assert.False(harness.Tab.Commit.IsInserting);
    }

    [Fact]
    public void GivenInsertModeInTheBox_WhenAnotherTabIsShown_ThenInsertModeEnds()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.OpenOtherTab();

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Tab.Commit.IsInserting);
    }

    [Fact]
    public void GivenAnEmptyDraft_WhenTogglingAmend_ThenHeadsMessageFillsTheBoxAndTogglingOffRestoresTheEmptyDraft()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeToggleAmend);

        var read = harness.Take<ReadHeadMessageEffect>();
        Assert.Equal(harness.Tab.Head!.CommitHash, read.Hash);
        harness.Send(new HeadMessageRead(harness.TabId, read.Hash, "Previous subject\n\nPrevious body"));
        Assert.True(harness.Draft.Amend);
        Assert.Equal("Previous subject\n\nPrevious body", harness.Draft.Editor.Text);
        Assert.StartsWith("Amending ", harness.Draft.Title, StringComparison.Ordinal);
        Assert.Contains("Previous subject", harness.Draft.Title, StringComparison.Ordinal);

        harness.Run(ActionIds.WorkingTreeToggleAmend);

        Assert.False(harness.Draft.Amend);
        Assert.True(harness.Draft.Editor.IsBlank);
        Assert.Equal("Commit message", harness.Draft.Title);
        Assert.Equal(CommitTexts.AmendOffNotice, harness.LastStatus);
    }

    [Fact]
    public void GivenAmendSwitchedOffBeforeTheMessageArrives_WhenItArrives_ThenItIsIgnored()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Run(ActionIds.WorkingTreeToggleAmend);

        harness.Send(new HeadMessageRead(harness.TabId, "abc", "Late"));

        Assert.True(harness.Draft.Editor.IsBlank);
    }

    [Fact]
    public void GivenTheHeadMessageCannotBeRead_WhenTheFailureArrives_ThenTheStatusSaysSo()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Send(new HeadMessageReadFailed(harness.TabId, "boom"));

        Assert.Equal("Could not read the last commit's message: boom", harness.LastStatus);
        Assert.Equal(StatusSeverity.Error, harness.Effects.OfType<PostStatusEffect>().Last().Severity);
    }

    [Fact]
    public void GivenAMergeInProgress_WhenTogglingAmend_ThenItIsRefused()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetOperation(RepositoryOperation.Merge);

        harness.Run(ActionIds.WorkingTreeToggleAmend);

        Assert.Equal(CommitCheck.AmendDuringMerge, harness.LastStatus);
        Assert.False(harness.Draft.Amend);
        Assert.Empty(harness.Effects.OfType<ReadHeadMessageEffect>());
    }

    [Fact]
    public void GivenAnUnbornBranch_WhenTogglingAmend_ThenItIsRefused()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Load(head: HeadKind.Unborn);

        harness.Run(ActionIds.WorkingTreeToggleAmend);

        Assert.Equal(CommitCheck.NothingToAmend, harness.LastStatus);
    }

    [Fact]
    public void GivenAmend_WhenTheCommitSucceeds_ThenTheStatusNamesThePreviousCommitAndTheUndo()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("Reworded");
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Run(ActionIds.WorkingTreeCommit);
        var effect = harness.Take<RunCommitEffect>();

        harness.Send(new CommitSucceeded(harness.TabId, effect.Request, new CommitResult("2222222222", "1111111111"), null));

        Assert.True(effect.Request.Amend);
        Assert.Contains("previous commit 1111111", harness.LastStatus, StringComparison.Ordinal);
        Assert.Contains("git reset --soft 1111111", harness.LastStatus, StringComparison.Ordinal);
        Assert.False(harness.Draft.Amend);
    }

    [Fact]
    public void GivenHeadIsOnTheUpstream_WhenAmending_ThenTheEffectNamesTheUpstreamAndTheStatusWarnsOfTheForcePush()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Load(upstream: "origin/main", ahead: 0);
        harness.SetText("Reworded");
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Run(ActionIds.WorkingTreeCommit);
        var effect = harness.Take<RunCommitEffect>();
        Assert.Equal("origin/main", effect.ForcePushUpstream);

        harness.Send(
            new CommitSucceeded(harness.TabId, effect.Request, new CommitResult("2222222222", "1111111111"), effect.ForcePushUpstream));

        Assert.Contains("force push", harness.LastStatus, StringComparison.Ordinal);
        Assert.Contains("origin/main", harness.LastStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOptionActions_WhenToggling_ThenTheDraftFollows()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        Assert.Equal("SignOff on", harness.LastStatus);
        harness.Run(ActionIds.WorkingTreeToggleNoVerify);
        Assert.Equal("No-Verify on: hooks are skipped", harness.LastStatus);
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        Assert.Equal("SignOff off", harness.LastStatus);
        harness.Run(ActionIds.WorkingTreeToggleNoVerify);
        Assert.Equal("No-Verify off: hooks run", harness.LastStatus);

        Assert.False(harness.Draft.SignOff);
        Assert.False(harness.Draft.NoVerify);
    }

    [Fact]
    public void GivenAMergeInProgress_WhenWritingTheMessage_ThenTheBoxStartsWithTheMergeMessageAndUntouchedItIsNotUnsent()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetOperation(RepositoryOperation.Merge);

        harness.Run(ActionIds.WorkingTreeWriteMessage);

        Assert.Equal(harness.TabId, harness.Take<ReadMergeMessageEffect>().TabId);
        harness.Send(new MergeMessageRead(harness.TabId, "Merge branch 'other'"));
        Assert.Equal("Merge branch 'other'", harness.Draft.Editor.Text);
        Assert.Empty(CommitRules.SessionDrafts(harness.State.Workspace));

        harness.Type(" edited");

        Assert.Single(CommitRules.SessionDrafts(harness.State.Workspace));
    }

    [Fact]
    public void GivenTypedTextBeforeTheMergeMessageArrives_WhenItArrives_ThenTheTextIsKept()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetOperation(RepositoryOperation.Merge);
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("mine");

        harness.Send(new MergeMessageRead(harness.TabId, "Merge branch 'other'"));

        Assert.Equal("mine", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAMessage_WhenClickingAnotherLine_ThenTheCursorLandsThere()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("first line\nsecond line");

        harness.Send(new CommitCursorPlaced(harness.TabId, 0, 3));

        Assert.Equal((0, 3), (harness.Draft.Editor.CursorRow, harness.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenALineBeyondTheEnd_WhenClicking_ThenTheLastLineIsUsed()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("a\nb\nc\nd");

        harness.Send(new CommitCursorPlaced(harness.TabId, 9, 0));

        Assert.Equal(3, harness.Draft.Editor.CursorRow);
    }

    [Fact]
    public void GivenABlankDraftOrNone_WhenClicking_ThenTheCursorStaysAtTheStartAndNoDraftIsMade()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));

        harness.Send(new CommitCursorPlaced(harness.TabId, 3, 5));

        Assert.Null(harness.Tab.Commit.Draft);
    }

    [Fact]
    public void GivenACommitIsRunning_WhenClicking_ThenTheCursorDoesNotMove()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("abc\ndef");
        harness.Run(ActionIds.WorkingTreeCommit);

        harness.Send(new CommitCursorPlaced(harness.TabId, 0, 0));

        Assert.Equal((1, 3), (harness.Draft.Editor.CursorRow, harness.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenTypedMessageAndOptions_WhenTakenForTheSessionAndRestored_ThenTheDraftComesBack()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Subject");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);

        var saved = CommitRules.SessionDrafts(harness.State.Workspace);
        harness.SetText(string.Empty);
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        harness.Send(new CommitDraftRestored(harness.TabId, saved[harness.TabId]));

        Assert.Equal(new SessionDraft("Subject", true, false), Assert.Single(saved).Value);
        Assert.Equal("Subject", harness.Draft.Editor.Text);
        Assert.True(harness.Draft.SignOff);
        Assert.True(CommitRules.HasUnsentMessage(harness.State.Workspace, harness.TabId));
    }

    [Fact]
    public void GivenAmendWithAnEditedMessage_WhenTakenForTheSession_ThenTheMessageIsNotSaved()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Send(new HeadMessageRead(harness.TabId, "1111111", "HEAD message"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type(" edited");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);

        var saved = Assert.Single(CommitRules.SessionDrafts(harness.State.Workspace)).Value;

        Assert.Equal(new SessionDraft(string.Empty, true, false), saved);
    }

    [Fact]
    public void GivenTheQuitWasWarned_WhenTheDraftChanges_ThenTheWarningIsReset()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.WarnQuit();

        harness.Type("x");

        Assert.False(harness.State.Shell.QuitWarned);
    }

    [Fact]
    public void GivenInsertModeInTheBox_WhenItsTabIsClosed_ThenInsertModeEnds()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.OpenOtherTab();
        harness.Activate(harness.TabId);
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        Assert.Equal(KeyMode.Insert, harness.Mode);

        harness.Send(new CloseTabCommand(harness.TabId));

        Assert.Equal(KeyMode.Normal, harness.Mode);
    }
}
