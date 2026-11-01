using Gitunia.App.Shell;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Tests.Fakes;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class CommitActionsTests
{
    [Fact]
    public void GivenTheLists_WhenWritingTheMessage_ThenTheBoxHasFocusAndInsertModeIsOn()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeWriteMessage);

        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        Assert.True(harness.Commits.IsEditing);
    }

    [Fact]
    public void GivenInsertMode_WhenTypingKeysAndNewLines_ThenTheDraftHoldsTheMessage()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Type("Subject");
        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Run(ActionIds.WorkingTreeMessageNewLine);
        harness.Type("Body ünï");

        Assert.Equal("Subject\n\nBody ünï", harness.Draft.Editor.Text);
        Assert.Single(harness.Commits.SnapshotForSession());
    }

    [Fact]
    public void GivenNoInsertMode_WhenAKeyIsTyped_ThenTheBoxDoesNotTakeIt()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        Assert.False(harness.Commits.HandleTextKey(new KeyEvent("x")));
    }

    [Fact]
    public void GivenAnotherPromptInInsertMode_WhenEnterArrives_ThenItIsPassedOnAsAccept()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Run(ActionIds.Cancel);
        harness.Mode = KeyMode.Insert;

        harness.Run(ActionIds.WorkingTreeMessageNewLine);

        Assert.Equal([ActionIds.InputAccept], harness.Routed);
    }

    [Fact]
    public void GivenAnEmptyMessage_WhenCommitting_ThenItIsRefusedWithoutRunningGit()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        var notice = harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal(CommitCheck.EmptyMessage, notice);
        Assert.Empty(harness.Writer.Requests);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenNothingStaged_WhenCommitting_ThenItIsRefusedUnlessAMergeIsInProgress()
    {
        using var harness = new Harness(Modified("a.txt"));
        harness.Draft.Editor.SetText("Message");

        Assert.Equal(CommitCheck.NothingStaged, harness.Run(ActionIds.WorkingTreeCommit));
        Assert.Empty(harness.Writer.Requests);

        harness.Operations.Operation = RepositoryOperation.Merge;
        Assert.Equal(string.Empty, harness.Run(ActionIds.WorkingTreeCommit));
    }

    [Fact]
    public async Task GivenAMessageAndOptions_WhenCommitting_ThenGitGetsThemAndTheDraftIsClearedAndFocusReturnsToTheLists()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Fix the thing");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        harness.Run(ActionIds.WorkingTreeToggleNoVerify);

        harness.Run(ActionIds.WorkingTreeCommit);
        Assert.True(harness.Draft.IsCommitting);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        var request = Assert.Single(harness.Writer.Requests);
        Assert.Equal(("Fix the thing", true, true, false), (request.Message, request.SignOff, request.NoVerify, request.Amend));
        Assert.True(harness.Draft.Editor.IsBlank);
        Assert.True(harness.Draft.SignOff);
        Assert.Equal("Committed 2222222", harness.Statuses.Log.Latest!.Text);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenARunningCommit_WhenItRuns_ThenTheGateMarksTheTabUntilItEnds()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Gated");

        harness.Run(ActionIds.WorkingTreeCommit);
        var busyWhileRunning = harness.Busy.IsBusy(harness.Tab.Id);
        await harness.Pump.RunUntilAsync(() => !harness.Busy.IsAnyBusy);

        Assert.True(busyWhileRunning);
        Assert.False(harness.Draft.IsCommitting);
    }

    [Fact]
    public async Task GivenAFailingHook_WhenCommitting_ThenTheDraftIsKeptAndTheOutputAndHintAreShown()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.Failure = new GitCommandFailedException(["commit"], 1, "lint failed: a.txt\nfix it\n");
        harness.Draft.Editor.SetText("Message");

        harness.Run(ActionIds.WorkingTreeCommit);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        Assert.Equal("Message", harness.Draft.Editor.Text);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest!.Severity);
        Assert.Equal("Commit failed: lint failed: a.txt", harness.Statuses.Log.Latest.Text);
        var output = Assert.Single(harness.Outputs);
        Assert.Equal(CommitFailureReport.Title, output.Title);
        Assert.Equal(CommitFailureReport.Heading, output.Heading);
        Assert.Contains("lint failed: a.txt", output.Lines, StringComparer.Ordinal);
        Assert.Contains("fix it", output.Lines, StringComparer.Ordinal);
        Assert.Contains("<space>ec", output.Lines[^1], StringComparison.Ordinal);
        Assert.Equal(1, harness.Refreshes);
    }

    [Fact]
    public async Task GivenGitPrintingOnStdout_WhenCommitting_ThenTheOverlayAndNoticeIncludeIt()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.Failure = new GitCommandFailedException(["commit"], 1, string.Empty, "nothing to commit, working tree clean\n");
        harness.Draft.Editor.SetText("Message");

        harness.Run(ActionIds.WorkingTreeCommit);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        Assert.Equal("Commit failed: nothing to commit, working tree clean", harness.Statuses.Log.Latest!.Text);
        var output = Assert.Single(harness.Outputs);
        Assert.Equal("nothing to commit, working tree clean", output.Lines[0]);
    }

    [Fact]
    public async Task GivenACommitThatFailsOnABackgroundTab_WhenTheTabIsShown_ThenItsOutputOpensOnce()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        var tabId = harness.Tab.Id;
        harness.Writer.Failure = new GitCommandFailedException(["commit"], 1, "lint failed: a.txt\n");
        harness.Draft.Editor.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        harness.OpenOtherTab();

        await harness.Pump.RunUntilAsync(() => !harness.Commits.DraftFor(tabId).IsCommitting);
        harness.Commits.ShowPendingFailure();

        Assert.Empty(harness.Outputs);
        Assert.Equal("Commit failed: lint failed: a.txt", harness.Statuses.Log.Latest!.Text);

        harness.Activate(tabId);
        harness.Commits.ShowPendingFailure();
        harness.Commits.ShowPendingFailure();

        var output = Assert.Single(harness.Outputs);
        Assert.Contains("lint failed: a.txt", output.Lines, StringComparer.Ordinal);
    }

    [Fact]
    public async Task GivenAHookThatWantsInput_WhenCommitting_ThenTheEmptyOutputStillGetsTheHint()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.Failure = new GitCommandFailedException(["commit"], 1, string.Empty);
        harness.Draft.Editor.SetText("Message");

        harness.Run(ActionIds.WorkingTreeCommit);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        var output = Assert.Single(harness.Outputs);
        Assert.Equal(CommitFailureReport.NoOutput, output.Lines[0]);
        Assert.Contains("passphrase", output.Lines[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("navigate.back")]
    public async Task GivenARunningCommit_WhenEscapeIsPressed_ThenItIsCancelledAndTheDraftStays(string escapeAction)
    {
        // Esc is Cancel in Insert mode and NavigateBack in the box's Normal mode: both stop the commit.
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.IsHeld = true;
        harness.Draft.Editor.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal("A commit is running: Esc cancels it", harness.Run(ActionIds.WorkingTreeCommit));
        harness.Run(new ActionId(escapeAction));
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        Assert.Equal("Message", harness.Draft.Editor.Text);
        Assert.StartsWith("Commit cancelled", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Empty(harness.Outputs);
        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public async Task GivenARunningCommit_WhenTheTabIsClosed_ThenTheCommitIsCancelledAndNothingThrows()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.IsHeld = true;
        harness.Draft.Editor.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);

        harness.CloseTab();
        harness.Commits.ForgetClosedTabs();
        await harness.Pump.RunPostsAsync(1);

        Assert.Empty(harness.Commits.SnapshotForSession());
    }

    [Fact]
    public void GivenARunningCommit_WhenEnterIsPressedInTheBox_ThenTheMessageIsNotEdited()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Message");
        harness.Writer.IsHeld = true;
        harness.Run(ActionIds.WorkingTreeCommit);

        harness.Run(ActionIds.WorkingTreeMessageNewLine);

        Assert.Equal("Message", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenInsertMode_WhenEscapeIsPressed_ThenNormalModeStartsAndTheBoxKeepsFocus()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Run(ActionIds.Cancel);

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.Equal(PaneKind.WorkingMessage, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenUsingMotionsAndDeletes_ThenTheTextChangesAndTheBoxKeepsFocus()
    {
        using var harness = new Harness(StagedModified("a.txt"));
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
        Assert.True(harness.Commits.ShowsCursor);
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenAppendingAndOpeningALine_ThenInsertModeStartsAtThePlaceVimWould()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("ab");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeMessageLineStart);

        harness.Run(ActionIds.WorkingTreeMessageAppend);
        harness.Type("X");
        harness.Mode = KeyMode.Normal;
        harness.Run(ActionIds.WorkingTreeMessageOpenLine);
        harness.Type("Y");

        Assert.Equal("aXb\nY", harness.Draft.Editor.Text);
        Assert.Equal(KeyMode.Insert, harness.Mode);
    }

    [Fact]
    public void GivenAnInsertSession_WhenUndoingInNormalMode_ThenTheWholeSessionIsUndone()
    {
        using var harness = new Harness(StagedModified("a.txt"));
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
    public void GivenAnEarlierChange_WhenInsertEndsWithNothingTyped_ThenUndoRevertsTheEarlierChange()
    {
        using var harness = new Harness(StagedModified("a.txt"));
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
        using var harness = new Harness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Draft.Editor.SetText("keep");

        harness.Run(ActionIds.WorkingTreeMessageDeleteLine);

        Assert.Equal("keep", harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnotherPromptOwnsInsertMode_WhenTheBoxIsFocused_ThenTheBoxShowsNoCursor()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Send(new FocusPaneCommand(PaneKind.WorkingMessage));

        harness.Mode = KeyMode.Insert;

        Assert.False(harness.Commits.ShowsCursor);
    }

    [Fact]
    public void GivenALongLineAndNormalMode_WhenClickingPastTheBoxWidth_ThenTheCursorLandsWhereTheTextIsScrolledTo()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type(new string('x', 30));
        harness.Run(ActionIds.Cancel);

        var offset = MessageEditorRenderer.ScrollOffset(harness.Draft.Editor, 10);

        harness.Commits.PlaceCursor(2, 0, 10);

        var expected = offset + 2;
        Assert.True(expected > 2);
        Assert.Equal(expected, harness.Draft.Editor.CursorColumn);
    }

    [Fact]
    public void GivenTheBoxInNormalMode_WhenGoingBack_ThenTheListWithTheSelectionHasFocus()
    {
        using var harness = new Harness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Send(new SelectWorkingTreeFileCommand(WorkingTreeSection.Staged, 0));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Run(ActionIds.Cancel);

        Assert.True(harness.Commits.TryExecute(harness.Tab, ActionIds.NavigateBack));

        Assert.Equal(PaneKind.WorkingStaged, harness.Tab.View.FocusedPane);
    }

    [Fact]
    public void GivenAnEarlierChange_WhenInsertEndsByAFocusMoveWithNothingTyped_ThenUndoRevertsTheEarlierChange()
    {
        using var harness = new Harness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("first");
        harness.Run(ActionIds.Cancel);
        harness.Run(ActionIds.WorkingTreeEditMessage);
        harness.Send(new FocusPaneCommand(PaneKind.WorkingUnstaged));
        harness.Commits.LeaveInsertWhenFocusMoved();
        harness.Send(new FocusPaneCommand(PaneKind.WorkingMessage));

        harness.Run(ActionIds.WorkingTreeMessageUndo);

        Assert.Equal(string.Empty, harness.Draft.Editor.Text);
    }

    [Fact]
    public void GivenAnEarlierChange_WhenDeleteCharAndDeleteLineDoNothing_ThenOneUndoRevertsTheEarlierChange()
    {
        using var harness = new Harness(StagedModified("a.txt"));
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
        using var harness = new Harness(Modified("a.txt"), StagedModified("b.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);

        harness.Send(new FocusPaneCommand(PaneKind.WorkingUnstaged));
        harness.Commits.LeaveInsertWhenFocusMoved();

        Assert.Equal(KeyMode.Normal, harness.Mode);
        Assert.False(harness.Commits.HandleTextKey(new KeyEvent("x")));
    }

    [Fact]
    public async Task GivenAnEmptyDraft_WhenTogglingAmend_ThenHeadsMessageFillsTheBoxAndTogglingOffRestoresTheEmptyDraft()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        harness.Run(ActionIds.WorkingTreeToggleAmend);
        await harness.Pump.RunPostsAsync(1);

        Assert.True(harness.Draft.Amend);
        Assert.Equal("Previous subject\n\nPrevious body", harness.Draft.Editor.Text);
        Assert.StartsWith("Amending ", harness.Draft.Title, StringComparison.Ordinal);
        Assert.Contains("Previous subject", harness.Draft.Title, StringComparison.Ordinal);

        harness.Run(ActionIds.WorkingTreeToggleAmend);

        Assert.False(harness.Draft.Amend);
        Assert.True(harness.Draft.Editor.IsBlank);
        Assert.Equal("Commit message", harness.Draft.Title);
    }

    [Fact]
    public void GivenAMergeInProgress_WhenTogglingAmend_ThenItIsRefused()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Operations.Operation = RepositoryOperation.Merge;

        var notice = harness.Run(ActionIds.WorkingTreeToggleAmend);

        Assert.Equal(CommitCheck.AmendDuringMerge, notice);
        Assert.False(harness.Draft.Amend);
    }

    [Fact]
    public async Task GivenAmend_WhenCommitting_ThenTheStatusNamesThePreviousCommitAndTheUndo()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("Reworded");
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        await harness.Pump.RunPostsAsync(1);

        harness.Run(ActionIds.WorkingTreeCommit);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        Assert.True(harness.Writer.Requests[0].Amend);
        var text = harness.Statuses.Log.Latest!.Text;
        Assert.Contains("previous commit 1111111", text, StringComparison.Ordinal);
        Assert.Contains("git reset --soft 1111111", text, StringComparison.Ordinal);
        Assert.False(harness.Draft.Amend);
    }

    [Fact]
    public async Task GivenHeadIsOnTheUpstream_WhenAmending_ThenTheStatusWarnsOfTheForcePush()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Load(upstream: "origin/main", ahead: 0);
        harness.Draft.Editor.SetText("Reworded");
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        await harness.Pump.RunPostsAsync(1);

        harness.Run(ActionIds.WorkingTreeCommit);
        await harness.Pump.RunUntilAsync(() => !harness.Draft.IsCommitting);

        Assert.Contains("force push", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Contains("origin/main", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheOptionActions_WhenToggling_ThenTheDraftFollows()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        Assert.Equal("SignOff on", harness.Run(ActionIds.WorkingTreeToggleSignOff));
        Assert.Equal("No-Verify on: hooks are skipped", harness.Run(ActionIds.WorkingTreeToggleNoVerify));
        Assert.Equal("SignOff off", harness.Run(ActionIds.WorkingTreeToggleSignOff));

        Assert.False(harness.Draft.SignOff);
        Assert.True(harness.Draft.NoVerify);
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenWritingTheMessage_ThenTheBoxStartsWithGitsMergeMessageAndUntouchedItIsNotUnsent()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Writer.MergeMessage = "Merge branch 'other'";
        harness.Operations.Operation = RepositoryOperation.Merge;

        harness.Run(ActionIds.WorkingTreeWriteMessage);
        await harness.Pump.RunPostsAsync(1);

        Assert.Equal("Merge branch 'other'", harness.Draft.Editor.Text);
        Assert.Empty(harness.Commits.SnapshotForSession());

        harness.Type(" edited");

        Assert.Single(harness.Commits.SnapshotForSession());
    }

    [Fact]
    public void GivenAClosedTab_WhenTheWorkspaceChanges_ThenItsDraftIsForgotten()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("Message");
        var tabId = harness.Tab.Id;

        harness.CloseTab();
        harness.Commits.ForgetClosedTabs();

        Assert.Null(harness.Commits.DraftOf(tabId));
    }

    private sealed record Output(string Title, string Heading, IReadOnlyList<string> Lines);

    [Fact]
    public void GivenAMessage_WhenClickingAnotherLine_ThenTheCursorLandsThereWithoutScrollOffset()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("first line\nsecond line");

        harness.Commits.PlaceCursor(3, 0, 20);

        Assert.Equal((0, 3), (harness.Draft.Editor.CursorRow, harness.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenAScrolledCursorLineWhileEditing_WhenClickingIt_ThenTheScrollOffsetIsAdded()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("abcdefghij");

        harness.Commits.PlaceCursor(1, 0, 4);

        Assert.Equal(8, harness.Draft.Editor.CursorColumn);
    }

    [Fact]
    public void GivenTheBoxIsScrolledDown_WhenClicking_ThenTheRowIsRelativeToTheFirstVisibleLine()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("a\nb\nc\nd");
        harness.Draft.Top = 2;

        harness.Commits.PlaceCursor(0, 1, 20);

        Assert.Equal(3, harness.Draft.Editor.CursorRow);
    }

    [Fact]
    public void GivenABlankDraft_WhenClicking_ThenTheCursorStaysAtTheStart()
    {
        using var harness = new Harness(StagedModified("a.txt"));

        harness.Commits.PlaceCursor(5, 3, 20);

        Assert.Equal((0, 0), (harness.Draft.Editor.CursorRow, harness.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenACommitIsRunning_WhenClicking_ThenTheCursorDoesNotMove()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("abc\ndef");
        harness.Draft.IsCommitting = true;

        harness.Commits.PlaceCursor(0, 0, 20);

        Assert.Equal((1, 3), (harness.Draft.Editor.CursorRow, harness.Draft.Editor.CursorColumn));
    }

    [Fact]
    public void GivenTypedMessageAndOptions_WhenSnapshottedAndRestored_ThenTheDraftComesBack()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        var raised = 0;
        harness.Commits.DraftChanged += () => raised++;
        harness.Run(ActionIds.WorkingTreeWriteMessage);
        harness.Type("Subject");
        harness.Run(ActionIds.WorkingTreeToggleSignOff);

        var saved = harness.Commits.SnapshotForSession();
        harness.Draft.Editor.Clear();
        harness.Draft.SignOff = false;
        harness.Commits.Restore(saved);

        Assert.True(raised >= 2);
        Assert.Equal(new SessionDraft("Subject", true, false), Assert.Single(saved).Value);
        Assert.Equal("Subject", harness.Draft.Editor.Text);
        Assert.True(harness.Draft.SignOff);
        Assert.True(harness.Commits.HasUnsentMessage(harness.Tab.Id));
    }

    [Fact]
    public void GivenDraftOnATab_WhenMovedToAnotherTab_ThenItIsOnTheNewTabAndNotTheOld()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("Keep me");
        var from = harness.Tab.Id;
        var to = new TabId(from.Value + 100);

        Assert.True(harness.Commits.MoveDraft(from, to));

        Assert.Equal("Keep me", harness.Commits.DraftOf(to)!.Editor.Text);
        Assert.Null(harness.Commits.DraftOf(from));
    }

    [Fact]
    public void GivenAmendDraft_WhenMovedToAnotherTab_ThenAmendAndItsMessageStayBehind()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.SetAmend(true);
        harness.Draft.Editor.SetText("HEAD message");
        harness.Draft.SignOff = true;
        var to = new TabId(harness.Tab.Id.Value + 100);

        Assert.True(harness.Commits.MoveDraft(harness.Tab.Id, to));

        var moved = harness.Commits.DraftOf(to)!;
        Assert.False(moved.Amend);
        Assert.True(moved.SignOff);
        Assert.False(moved.HasUnsentText);
    }

    [Fact]
    public void GivenTargetHasItsOwnMessage_WhenMovingADraft_ThenNothingMoves()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.Editor.SetText("Mine");
        var from = harness.Tab.Id;
        var to = new TabId(from.Value + 100);
        harness.Commits.DraftFor(to).Editor.SetText("Theirs");

        Assert.False(harness.Commits.MoveDraft(from, to));

        Assert.Equal("Mine", harness.Commits.DraftOf(from)!.Editor.Text);
        Assert.Equal("Theirs", harness.Commits.DraftOf(to)!.Editor.Text);
    }

    [Fact]
    public void GivenAmendWithAnEditedMessage_WhenSnapshotting_ThenTheMessageIsNotSaved()
    {
        using var harness = new Harness(StagedModified("a.txt"));
        harness.Draft.SetAmend(true);
        harness.Draft.Editor.SetText("Edited HEAD message");
        harness.Draft.SignOff = true;

        var saved = Assert.Single(harness.Commits.SnapshotForSession()).Value;

        Assert.Equal(new SessionDraft(string.Empty, true, false), saved);
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestShell _shell = new();

        public Harness(params StatusEntry[] entries)
        {
            GitDirectory = Path.Combine(Path.GetTempPath(), $"gitunia-commit-actions-{Guid.NewGuid():N}");
            Directory.CreateDirectory(GitDirectory);
            var location = new RepositoryLocation(GitDirectory, GitDirectory, GitDirectory, false, false);
            _shell.Host.Dispatch(new OpenRepositoryCommand(location));
            Entries = entries;
            Load();
            Send(new OpenWorkingTreeCommand());
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Commits = new CommitActions(
                _shell.Host,
                Writer,
                Operations,
                Statuses,
                Pump.Post,
                RefreshStatus,
                _ => TestContext.Current.CancellationToken,
                Busy,
                () => Mode,
                (action, _) => Routed.Add(action),
                () => { });
        }

        public string GitDirectory { get; }

        public StatusEntry[] Entries { get; }

        public UiPump Pump { get; }

        public FakeCommitWriter Writer { get; } = new();

        public FakeOperationDetector Operations { get; } = new();

        public StatusReporter Statuses => _shell.Statuses;

        public TabOperationGate Busy { get; } = new();

        public CommitActions Commits { get; }

        public KeyMode Mode
        {
            get => _shell.Mode;
            set => _shell.Host.SetKeyMode(value);
        }

        public List<Output> Outputs =>
            [.. _shell.Outputs.Select(output => new Output(output.Title, output.Heading, output.Lines))];

        public List<ActionId> Routed { get; } = [];

        public int Refreshes { get; private set; }

        public RepositoryState Tab => _shell.Host.Workspace.ActiveTab!;

        public CommitDraft Draft => Commits.DraftFor(Tab.Id);

        public string Run(ActionId action)
        {
            Commits.TryExecute(Tab, action);
            return Commits.Notice;
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Commits.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        public void Send(RepositoryCommand command) =>
            _shell.Host.Dispatch(new TabCommand(_shell.Host.Workspace.ActiveTabId!.Value, command));

        public void Load(string? upstream = null, int? ahead = null)
        {
            var snapshot = Snapshot(Entries);
            var branch = snapshot.Branch with { UpstreamName = upstream, Ahead = ahead };
            Send(new WorkingTreeLoadedCommand(snapshot with { Branch = branch }));
        }

        public void OpenOtherTab()
        {
            var other = Path.Combine(GitDirectory, "other");
            Directory.CreateDirectory(other);
            _shell.Host.Dispatch(new OpenRepositoryCommand(new RepositoryLocation(other, other, other, false, false)));
        }

        public void Activate(TabId tabId) => _shell.Host.Dispatch(new ActivateTabCommand(tabId));

        public void CloseTab() => _shell.Host.Dispatch(new CloseTabCommand(_shell.Host.Workspace.ActiveTabId!.Value));

        public void Dispose()
        {
            if (Directory.Exists(GitDirectory))
            {
                Directory.Delete(GitDirectory, true);
            }
        }

        private bool RefreshStatus(TabId tabId, Action done)
        {
            Refreshes++;
            Pump.Post(done);
            return true;
        }
    }
}
