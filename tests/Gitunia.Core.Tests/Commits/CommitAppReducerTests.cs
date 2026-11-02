using Gitunia.Core.Commits;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Persistence;
using Gitunia.Core.Running;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces;
using Gitunia.Git.Commits.Models;
using Xunit;
using static Gitunia.Tui.Tests.Rendering.WorkingTree.WorkingTreeEntries;

namespace Gitunia.Core.Tests.Commits;

// The commit inside the root reducer: the stage-all question, exclusion of the other index writers, the running mark
// and a draft that moves to another tab.
public class CommitAppReducerTests
{
    [Fact]
    public void GivenNothingStagedButUnstagedFiles_WhenCommitting_ThenTheQuestionIsAskedAndNothingRuns()
    {
        var harness = new CommitHarness(Modified("a.txt"), Untracked("new.txt"));
        harness.SetText("Everything");

        harness.Run(ActionIds.WorkingTreeCommit);

        var ask = harness.Take<ShowConfirmEffect>();
        Assert.Contains("Nothing is staged. Stage all changes and commit?", ask.Prompt.Lines);
        Assert.Equal("Stage all and commit", ask.Prompt.YesLabel);
        Assert.Equal(CommitCheck.NothingStaged, ask.UnavailableNotice);
        Assert.Equal(new CommitStageAllConfirmed(harness.TabId, new CommitRequest("Everything", false, false, false)), ask.OnAccepted);
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
        Assert.False(harness.Tab.Running.IsBusy);
    }

    [Fact]
    public void GivenOnlyConflictedFiles_WhenCommitting_ThenThereIsNoQuestion()
    {
        var harness = new CommitHarness(Conflict("c.txt"));
        harness.SetText("Message");

        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Empty(harness.Effects.OfType<ShowConfirmEffect>());
        Assert.Equal(CommitCheck.NothingStaged, harness.LastStatus);
    }

    [Fact]
    public void GivenTheQuestionAnsweredYes_WhenReducing_ThenAllIsStagedAndTheCommitFollowsWithTheMessageThatWasAsked()
    {
        var harness = new CommitHarness(Modified("a.txt"), Untracked("new.txt"));
        harness.SetText("Everything");
        harness.Run(ActionIds.WorkingTreeCommit);
        var ask = harness.Take<ShowConfirmEffect>();
        harness.Send(new ConfirmRequestedCommand(ask.Prompt, ask.OnAccepted, null));
        harness.SetText("Edited after the question");

        harness.Send(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        var staging = harness.Take<StagePathsEffect>();
        var then = Assert.IsType<CommitAfterStaging>(staging.Follow.Then);
        Assert.Equal("Everything", then.Request.Message);
        Assert.True(harness.Tab.Running.IsRunning(RunningOperationKind.Staging));
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenAllWasStaged_WhenTheFollowUpArrives_ThenTheCommitStartsWithTheAskedRequest()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var request = new CommitRequest("Everything", false, true, false);

        harness.Send(new CommitAfterStaging(harness.TabId, request));

        Assert.Equal(request, harness.Take<RunCommitEffect>().Request);
        Assert.True(harness.Tab.Running.IsRunning(RunningOperationKind.Commit));
    }

    [Fact]
    public void GivenStagingLeftNothingStaged_WhenTheFollowUpArrives_ThenNothingStartsAndTheNoticeShows()
    {
        var harness = new CommitHarness(Modified("a.txt"));

        harness.Send(new CommitAfterStaging(harness.TabId, new CommitRequest("Msg", false, false, false)));

        Assert.Equal(CommitCheck.NothingStaged, harness.LastStatus);
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenTheQuestionAnsweredNo_WhenReducing_ThenTheDraftAndTheIndexAreUntouched()
    {
        var harness = new CommitHarness(Modified("a.txt"));
        harness.SetText("Keep me");
        harness.Run(ActionIds.WorkingTreeCommit);
        var ask = harness.Take<ShowConfirmEffect>();
        harness.Send(new ConfirmRequestedCommand(ask.Prompt, ask.OnAccepted, null));

        harness.Send(new ConfirmAnsweredCommand(ConfirmAnswer.No));

        Assert.Equal("Keep me", harness.Draft.Editor.Text);
        Assert.Empty(harness.Effects.OfType<StagePathsEffect>());
        Assert.False(harness.Tab.Running.IsBusy);
    }

    [Fact]
    public void GivenTheTabIsClosedWhileTheQuestionIsOpen_WhenYIsAnswered_ThenNothingIsStagedOrCommitted()
    {
        var harness = new CommitHarness(Modified("a.txt"));
        var tabId = harness.TabId;
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        var ask = harness.Take<ShowConfirmEffect>();
        harness.Send(new ConfirmRequestedCommand(ask.Prompt, ask.OnAccepted, null));
        harness.Send(new CloseTabCommand(tabId));
        Assert.NotNull(harness.State.Shell.Confirm);
        harness.Effects.Clear();

        harness.Send(new ConfirmAnsweredCommand(ConfirmAnswer.Yes));

        Assert.Null(harness.State.Shell.Confirm);
        Assert.Empty(harness.Effects.OfType<StagePathsEffect>());
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenNothingStagedAndAmendOn_WhenCommitting_ThenThereIsNoQuestion()
    {
        var harness = new CommitHarness(Modified("a.txt"));
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Send(new HeadMessageRead(harness.TabId, null, "First"));
        harness.SetText("Reworded");

        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Empty(harness.Effects.OfType<ShowConfirmEffect>());
        Assert.True(harness.Take<RunCommitEffect>().Request.Amend);
    }

    [Theory]
    [InlineData(RunningOperationKind.Staging)]
    [InlineData(RunningOperationKind.Discard)]
    [InlineData(RunningOperationKind.Stash)]
    [InlineData(RunningOperationKind.Rebase)]
    [InlineData(RunningOperationKind.Integration)]
    [InlineData(RunningOperationKind.Pick)]
    [InlineData(RunningOperationKind.Branches)]
    public void GivenAnotherIndexWriterRuns_WhenCommitting_ThenItIsRefusedAndNothingStarts(RunningOperationKind kind)
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("Message");
        harness.Mark(kind);

        harness.Run(ActionIds.WorkingTreeCommit);

        Assert.Equal("Busy: another operation of this tab is still running", harness.LastStatus);
        Assert.Empty(harness.Effects.OfType<RunCommitEffect>());
    }

    [Fact]
    public void GivenACommitRuns_WhenStagingIsAsked_ThenItIsRefused()
    {
        var harness = new CommitHarness(Modified("a.txt"));
        harness.Mark(RunningOperationKind.Commit);

        harness.Send(new StageAllThenRequested(harness.TabId, new CommitAfterStaging(harness.TabId, new CommitRequest("m", false, false, false))));

        Assert.Empty(harness.Effects.OfType<StagePathsEffect>());
        Assert.Contains("Busy", harness.LastStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenACommitRuns_WhenAnotherWriterIsAsked_ThenItIsRefusedAndTheMarkIsFreedByTheResult()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        harness.SetText("Message");
        harness.Run(ActionIds.WorkingTreeCommit);
        var request = harness.Take<RunCommitEffect>().Request;
        Assert.True(harness.Tab.Running.IsRunning(RunningOperationKind.Commit));

        harness.Send(new CommitSucceeded(harness.TabId, request, new CommitResult("2222222", null), null));

        Assert.False(harness.Tab.Running.IsRunning(RunningOperationKind.Commit));
    }

    [Fact]
    public void GivenDraftOnATab_WhenMovedToAnotherTab_ThenItIsOnTheNewTabAndNotTheOld()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var from = harness.TabId;
        harness.SetText("Keep me");
        var to = harness.OpenOtherTab();

        harness.Send(new CommitDraftMoveRequested(from, to));

        Assert.Equal("Keep me", harness.State.Workspace.FindTab(to)!.Commit.Draft!.Editor.Text);
        Assert.Null(harness.State.Workspace.FindTab(from)?.Commit.Draft);
    }

    [Fact]
    public void GivenAmendDraft_WhenMovedToAnotherTab_ThenAmendAndItsMessageStayBehind()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var from = harness.TabId;
        harness.Run(ActionIds.WorkingTreeToggleAmend);
        harness.Send(new HeadMessageRead(from, "1111111", "HEAD message"));
        harness.Run(ActionIds.WorkingTreeToggleSignOff);
        var to = harness.OpenOtherTab();

        harness.Send(new CommitDraftMoveRequested(from, to));

        var moved = harness.State.Workspace.FindTab(to)!.Commit.Draft!;
        Assert.False(moved.Amend);
        Assert.True(moved.SignOff);
        Assert.False(moved.HasUnsentText);
    }

    [Fact]
    public void GivenTargetHasItsOwnMessage_WhenMovingADraft_ThenNothingMoves()
    {
        var harness = new CommitHarness(StagedModified("a.txt"));
        var from = harness.TabId;
        harness.SetText("Mine");
        var to = harness.OpenOtherTab();
        harness.Send(new CommitDraftRestored(to, new SessionDraft("Theirs", false, false)));

        harness.Send(new CommitDraftMoveRequested(from, to));

        Assert.Equal("Mine", harness.State.Workspace.FindTab(from)!.Commit.Draft!.Editor.Text);
        Assert.Equal("Theirs", harness.State.Workspace.FindTab(to)!.Commit.Draft!.Editor.Text);
    }
}
