using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Refs.Models;
using Xunit;
using static Gitunia.Core.Tests.Worktrees.WorktreeTestData;

namespace Gitunia.Core.Tests.Worktrees;

// Add, lock, unlock, prune and the results: (command, context) -> effects, with no terminal and no git.
public class WorktreeReducerTests
{
    private static readonly WorktreeTarget Nothing = new(null, null, null, null, null);

    [Fact]
    public void GivenAnAddRequest_WhenReducing_ThenTheBranchPromptOpensForTheTab()
    {
        var update = Reduce(new WorktreeAddRequested(Tab, Nothing), Context());

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("New worktree", prompt.Title);
        Assert.Equal("Branch: an existing one, or a new name", prompt.Context);
        Assert.Equal(Tab, prompt.Owner);
        Assert.Equal(new WorktreeBranchAnswer(Tab, null), prompt.OnAccepted);
    }

    [Fact]
    public void GivenAWorktreeRowWithACommit_WhenAdding_ThenThatCommitIsTheStartPointOverTheGraphSelection()
    {
        var target = new WorktreeTarget(SidebarRowKind.Worktree, "wt", Sibling("wt"), Topic, Main);

        var update = Reduce(new WorktreeAddRequested(Tab, target), Context());

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(new WorktreeBranchAnswer(Tab, Topic), prompt.OnAccepted);
    }

    [Fact]
    public void GivenAStashRowSelected_WhenAdding_ThenTheStartPointIgnoresTheStashCommit()
    {
        var target = new WorktreeTarget(SidebarRowKind.Stash, "stash@{0}", null, Topic, Main);

        var update = Reduce(new WorktreeAddRequested(Tab, target), Context());

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(new WorktreeBranchAnswer(Tab, Main), prompt.OnAccepted);
    }

    [Fact]
    public void GivenARunningOperation_WhenAdding_ThenItIsRefusedWithoutAPrompt()
    {
        var update = Reduce(new WorktreeAddRequested(Tab, Nothing), Context(busy: true));

        Assert.Equal(WorktreeTexts.BusyNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenANewBranchName_WhenTheBranchPromptIsAccepted_ThenThePathPromptStartsAtTheSelectedCommit()
    {
        var update = Reduce(new WorktreeBranchAccepted(Tab, Topic, "  fresh "), Context(refs: [Local("main", true)]));

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Worktree path", prompt.Title);
        Assert.Equal("Directory for fresh (new, at 2222222)", prompt.Context);
        Assert.Equal(Sibling("fresh"), prompt.InitialText);
        Assert.Equal(new WorktreePathAnswer(Tab, "fresh", Topic, false), prompt.OnAccepted);
    }

    [Fact]
    public void GivenNoSelectionAndAHead_WhenTheBranchIsNew_ThenTheStartPointIsTheHeadCommit()
    {
        var head = new HeadState(HeadKind.Branch, "main", Main);

        var update = Reduce(new WorktreeBranchAccepted(Tab, null, "fresh"), Context(head: head));

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(new WorktreePathAnswer(Tab, "fresh", Main, false), prompt.OnAccepted);
    }

    [Fact]
    public void GivenNoSelectionAndNoHead_WhenTheBranchIsNew_ThenGitStartsAtHead()
    {
        var update = Reduce(new WorktreeBranchAccepted(Tab, null, "fresh"), Context());

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(new WorktreePathAnswer(Tab, "fresh", "HEAD", false), prompt.OnAccepted);
    }

    [Fact]
    public void GivenAnExistingFreeBranch_WhenTheBranchPromptIsAccepted_ThenThePathPromptHasNoStartPoint()
    {
        var update = Reduce(new WorktreeBranchAccepted(Tab, Topic, "topic"), Context(refs: [Local("topic")]));

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Directory for topic (existing)", prompt.Context);
        Assert.Equal(new WorktreePathAnswer(Tab, "topic", null, false), prompt.OnAccepted);
    }

    [Fact]
    public void GivenABranchWithASlash_WhenTheBranchPromptIsAccepted_ThenTheDefaultDirectoryHasNoSeparator()
    {
        var update = Reduce(new WorktreeBranchAccepted(Tab, null, "feature/x"), Context());

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal(Sibling("feature-x"), prompt.InitialText);
    }

    [Fact]
    public void GivenABranchHeldByAnotherWorktree_WhenTheBranchPromptIsAccepted_ThenItIsRefusedBeforeThePathPrompt()
    {
        var refs = new[] { Local("topic", checkedOutIn: Sibling("other")) };

        var update = Reduce(new WorktreeBranchAccepted(Tab, null, "topic"), Context(refs: [.. refs]));

        Assert.Equal(
            $"topic is already checked out in {Sibling("other")}: a branch can be in one worktree only",
            Warning(update.Effects));
    }

    [Fact]
    public void GivenABlankBranch_WhenTheBranchPromptIsAccepted_ThenNothingIsAdded()
    {
        var update = Reduce(new WorktreeBranchAccepted(Tab, null, "   "), Context());

        Assert.Equal("No branch given: nothing was added", Warning(update.Effects));
    }

    [Fact]
    public void GivenAPath_WhenThePathPromptIsAccepted_ThenTheAddStartsWithTheResolvedDirectory()
    {
        var update = Reduce(new WorktreePathAccepted(Tab, "fresh", Topic, false, " ../elsewhere "), Context());

        var add = Assert.IsType<AddWorktreeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Root)!, "..", "elsewhere")), add.Request.Path);
        Assert.Equal("fresh", add.Request.Branch);
        Assert.Equal(Topic, add.Request.NewBranchStartPoint);
        Assert.False(add.OpenAfter);
    }

    [Fact]
    public void GivenARelativeName_WhenThePathPromptIsAccepted_ThenItIsResolvedBesideTheTabsWorktree()
    {
        var update = Reduce(new WorktreePathAccepted(Tab, "fresh", null, true, "fresh"), Context());

        var add = Assert.IsType<AddWorktreeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Sibling("fresh"), add.Request.Path);
        Assert.Null(add.Request.NewBranchStartPoint);
        Assert.True(add.OpenAfter);
    }

    [Fact]
    public void GivenABlankPath_WhenThePathPromptIsAccepted_ThenNothingIsAdded()
    {
        var update = Reduce(new WorktreePathAccepted(Tab, "fresh", null, false, " "), Context());

        Assert.Equal("No path given: nothing was added", Warning(update.Effects));
    }

    [Fact]
    public void GivenAnOperationStartedWhileThePathPromptWasOpen_WhenAccepted_ThenItIsRefused()
    {
        var update = Reduce(new WorktreePathAccepted(Tab, "fresh", null, false, "fresh"), Context(busy: true));

        Assert.Equal($"{WorktreeTexts.BusyNotice}; nothing was changed", Warning(update.Effects));
    }

    [Fact]
    public void GivenALocalBranchNoWorktreeHolds_WhenOpening_ThenOnlyThePathIsAskedAndTheWorktreeWillOpen()
    {
        var context = Context(refs: [Local("main", true), Local("topic")]);

        var update = Reduce(new WorktreeOpenRequested(Tab, Row(SidebarRowKind.LocalBranch, "topic")), context);

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal("Worktree path", prompt.Title);
        Assert.Equal(Sibling("topic"), prompt.InitialText);
        Assert.Equal(new WorktreePathAnswer(Tab, "topic", null, true), prompt.OnAccepted);
    }

    [Fact]
    public void GivenALocalBranchNoWorktreeHolds_WhenOpeningWhileBusy_ThenItIsRefused()
    {
        var context = Context(refs: [Local("topic")], busy: true);

        var update = Reduce(new WorktreeOpenRequested(Tab, Row(SidebarRowKind.LocalBranch, "topic")), context);

        Assert.Equal(WorktreeTexts.BusyNotice, Warning(update.Effects));
    }

    [Fact]
    public void GivenAnUnlockedLinkedWorktree_WhenLocking_ThenTheReasonPromptNamesIt()
    {
        var linked = Sibling("wt");
        var context = Context(MainAnd(Info(linked, "wt")));

        var update = Reduce(new WorktreeLockRequested(Tab, WorktreeRow(linked)), context);

        var prompt = Assert.IsType<ShowPromptEffect>(Assert.Single(update.Effects)).Request;
        Assert.Equal($"Reason for locking {linked}, optional", prompt.Context);
        Assert.Equal(new WorktreeLockAnswer(Tab, linked), prompt.OnAccepted);
    }

    [Fact]
    public void GivenTheMainOrALockedWorktreeOrNoRow_WhenLocking_ThenEachIsRefusedWithItsOwnReason()
    {
        var locked = Sibling("locked");
        var context = Context(MainAnd(Info(locked, "locked", locked: true)));

        Assert.Equal(
            "The main worktree cannot be locked",
            Warning(Reduce(new WorktreeLockRequested(Tab, WorktreeRow(Root)), context).Effects));
        Assert.Equal(
            $"{locked} is already locked",
            Warning(Reduce(new WorktreeLockRequested(Tab, WorktreeRow(locked)), context).Effects));
        Assert.Equal(
            WorktreeTexts.NoWorktreeNotice,
            Warning(Reduce(new WorktreeLockRequested(Tab, Nothing), context).Effects));
        Assert.Equal(
            WorktreeTexts.BusyNotice,
            Warning(Reduce(new WorktreeLockRequested(Tab, WorktreeRow(locked)), Context(busy: true)).Effects));
    }

    [Fact]
    public void GivenAReason_WhenTheLockPromptIsAccepted_ThenTheLockStartsWithTheTrimmedReason()
    {
        var update = Reduce(new WorktreeLockReasonAccepted(Tab, Sibling("wt"), " on a usb stick "), Context());

        var effect = Assert.IsType<LockWorktreeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Sibling("wt"), effect.Path);
        Assert.Equal("on a usb stick", effect.Reason);
    }

    [Fact]
    public void GivenAnUnlockedWorktree_WhenUnlocking_ThenItIsToldItIsNotLockedAndNothingRuns()
    {
        var linked = Sibling("wt");

        var context = Context(MainAnd(Info(linked, "wt")));

        var update = Reduce(new WorktreeUnlockRequested(Tab, WorktreeRow(linked)), context);

        Assert.Equal($"{linked} is not locked", Warning(update.Effects));
    }

    [Fact]
    public void GivenALockedWorktree_WhenUnlocking_ThenTheUnlockStarts()
    {
        var linked = Sibling("wt");

        var update = Reduce(
            new WorktreeUnlockRequested(Tab, WorktreeRow(linked)),
            Context(MainAnd(Info(linked, "wt", locked: true))));

        Assert.Equal(linked, Assert.IsType<UnlockWorktreeEffect>(Assert.Single(update.Effects)).Path);
    }

    [Fact]
    public void GivenPrunableAndLockedPrunableEntries_WhenPruning_ThenTheConfirmListsOnlyTheUnlockedOnes()
    {
        var gone = Sibling("gone");
        var heldGone = Sibling("held");
        var context = Context(MainAnd(Info(gone, "gone", prunable: true), Info(heldGone, "held", true, true)));

        var update = Reduce(new WorktreePruneRequested(Tab), context);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Contains(gone, confirm.Prompt.Lines);
        Assert.DoesNotContain(heldGone, confirm.Prompt.Lines);
        Assert.Equal("Prune 1 worktree entry whose directory is gone?", confirm.Prompt.Lines[0]);
        Assert.Equal(new WorktreePruneConfirmed(Tab), confirm.OnAccepted);
    }

    [Fact]
    public void GivenNothingPrunableOrABusyTab_WhenPruning_ThenNoDialogOpens()
    {
        Assert.Equal(
            "No worktree entry to prune",
            Warning(Reduce(new WorktreePruneRequested(Tab), Context(MainAnd())).Effects));
        Assert.Equal(
            WorktreeTexts.BusyNotice,
            Warning(Reduce(new WorktreePruneRequested(Tab), Context(busy: true)).Effects));
    }

    [Fact]
    public void GivenAConfirmedPrune_WhenReducing_ThenThePruneStartsUnlessTheTabBecameBusy()
    {
        var started = Reduce(new WorktreePruneConfirmed(Tab), Context());
        var refused = Reduce(new WorktreePruneConfirmed(Tab), Context(busy: true));

        Assert.IsType<PruneWorktreesEffect>(Assert.Single(started.Effects));
        Assert.Equal($"{WorktreeTexts.BusyNotice}; nothing was changed", Warning(refused.Effects));
    }

    [Fact]
    public void GivenASuccess_WhenReducing_ThenTheTextIsPostedAndTheTabRefreshedWithoutOpeningAnything()
    {
        var update = Reduce(new WorktreeOperationSucceeded(Tab, "Locked worktree x", null), Context());

        var post = Assert.IsType<PostStatusEffect>(update.Effects[0]);
        Assert.Equal(StatusSeverity.Info, post.Severity);
        Assert.Equal("Locked worktree x", post.Text);
        Assert.IsType<RefreshTabEffect>(update.Effects[1]);
        Assert.Equal(2, update.Effects.Length);
    }

    [Fact]
    public void GivenASuccessThatNamesANewWorktree_WhenReducing_ThenItIsLocatedToOpenAsATab()
    {
        var update = Reduce(new WorktreeOperationSucceeded(Tab, "Added", Sibling("wt")), Context());

        var locate = Assert.IsType<LocateWorktreeEffect>(update.Effects[2]);
        Assert.Equal(Sibling("wt"), locate.Path);
        Assert.False(locate.IsSwitch);
    }

    [Fact]
    public void GivenAFailureOrACancel_WhenReducing_ThenTheFailureIsAnErrorWithARefreshAndTheCancelIsSilent()
    {
        var failed = Reduce(new WorktreeOperationFailed(Tab, "Could not add the worktree: boom"), Context());
        var cancelled = Reduce(new WorktreeOperationCancelled(Tab), Context());

        Assert.Equal(StatusSeverity.Error, Assert.IsType<PostStatusEffect>(failed.Effects[0]).Severity);
        Assert.IsType<RefreshTabEffect>(failed.Effects[1]);
        Assert.Empty(cancelled.Effects);
    }

    [Fact]
    public void GivenALocateFailure_WhenReducing_ThenItIsShownAsAnError()
    {
        var update = Reduce(new WorktreeLocateFailed(Tab, "Could not open x: gone"), Context());

        var post = Assert.IsType<PostStatusEffect>(Assert.Single(update.Effects));
        Assert.Equal(StatusSeverity.Error, post.Severity);
    }
}
