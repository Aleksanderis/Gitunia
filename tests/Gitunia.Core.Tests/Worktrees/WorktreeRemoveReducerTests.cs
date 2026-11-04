using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Worktrees;
using Xunit;
using static Gitunia.Core.Tests.Worktrees.WorktreeTestData;

namespace Gitunia.Core.Tests.Worktrees;

// The removal of a worktree: refusals, the first confirm, the second one for uncommitted changes.
public class WorktreeRemoveReducerTests
{
    private static readonly string Linked = Sibling("wt");

    [Fact]
    public void GivenALinkedWorktree_WhenRemoving_ThenTheConfirmNamesItsPathBranchAndUndo()
    {
        var context = Context(MainAnd(Info(Linked, "wt")));

        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), context);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Remove worktree", confirm.Prompt.Title);
        Assert.Equal($"Remove the worktree {Linked}?", confirm.Prompt.Lines[0]);
        Assert.Equal("Its branch wt is kept.", confirm.Prompt.Lines[1]);
        Assert.Equal(
            "The branch stays; wt can be checked out again with a new worktree (<space>wa).",
            confirm.Prompt.Consequence);
        Assert.Equal(new WorktreeRemoveConfirmed(Tab, Linked), confirm.OnAccepted);
        Assert.Equal(WorktreeTexts.TooSmallNotice, confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenADetachedWorktree_WhenRemoving_ThenTheConfirmWarnsAboutCommitsNoBranchReaches()
    {
        var context = Context(MainAnd(Info(Linked)));

        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), context);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("It has a detached HEAD: commits that no branch reaches are lost.", confirm.Prompt.Lines[1]);
        Assert.Equal(
            "No undo is offered for commits that no branch reaches.",
            confirm.Prompt.Consequence);
    }

    [Fact]
    public void GivenTheMainWorktree_WhenRemoving_ThenItIsRefusedWithoutADialog()
    {
        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Root)), Context(MainAnd()));

        Assert.Equal("The main worktree cannot be removed", Warning(update.Effects));
    }

    [Fact]
    public void GivenATabOnTheLinkedWorktree_WhenRemovingItsOwnWorktree_ThenItIsRefused()
    {
        var tabOnLinked = TabOf(Tab, Linked, [Info(Root, "main", main: true), Info(Linked, "wt")]);
        var context = new WorktreeContext(tabOnLinked, [tabOnLinked], false);

        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), context);

        Assert.Equal("This tab is on that worktree: open another to remove it", Warning(update.Effects));
    }

    [Fact]
    public void GivenALockedWorktree_WhenRemoving_ThenItIsRefusedPointingToUnlock()
    {
        var context = Context(MainAnd(Info(Linked, "wt", locked: true)));

        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), context);

        Assert.Equal($"{Linked} is locked: unlock it first (<space>wu)", Warning(update.Effects));
    }

    [Fact]
    public void GivenAPrunableWorktree_WhenRemoving_ThenItIsRefusedPointingToPrune()
    {
        var context = Context(MainAnd(Info(Linked, "wt", prunable: true)));

        var update = Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), context);

        Assert.Equal($"{Linked} is missing on disk: prune it instead (<space>wp)", Warning(update.Effects));
    }

    [Fact]
    public void GivenNoWorktreeRowOrABusyTab_WhenRemoving_ThenTheNoticeIsShownAndNoDialogOpens()
    {
        var nothing = new WorktreeTarget(null, null, null, null, null);

        Assert.Equal(
            WorktreeTexts.NoWorktreeNotice,
            Warning(Reduce(new WorktreeRemoveRequested(Tab, nothing), Context(MainAnd())).Effects));
        Assert.Equal(
            WorktreeTexts.BusyNotice,
            Warning(Reduce(new WorktreeRemoveRequested(Tab, WorktreeRow(Linked)), Context(busy: true)).Effects));
    }

    [Fact]
    public void GivenTheConfirmedRemoval_WhenReducing_ThenTheRemovalStartsWithoutForce()
    {
        var update = Reduce(new WorktreeRemoveConfirmed(Tab, Linked), Context());

        var remove = Assert.IsType<RemoveWorktreeEffect>(Assert.Single(update.Effects));
        Assert.Equal(Linked, remove.Path);
        Assert.False(remove.Force);
    }

    [Fact]
    public void GivenAnOperationStartedWhileTheDialogWasOpen_WhenConfirmed_ThenNothingIsRemoved()
    {
        var update = Reduce(new WorktreeRemoveConfirmed(Tab, Linked), Context(busy: true));

        Assert.Equal($"{WorktreeTexts.BusyNotice}; nothing was removed", Warning(update.Effects));
    }

    [Fact]
    public void GivenADirtyWorktree_WhenGitFindsChanges_ThenTheSecondConfirmListsTheLossAndNamesAtMostFive()
    {
        ImmutableArray<string> changes = ["a", "b", "c", "d", "e", "f", "g"];

        var update = Reduce(new WorktreeRemoveNeedsForce(Tab, Linked, changes), Context());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(update.Effects));
        Assert.Equal("Remove worktree with changes", confirm.Prompt.Title);
        Assert.Equal(
            $"{Linked} has 7 changed or untracked paths: a, b, c, d, e and 2 more",
            confirm.Prompt.Lines[0]);
        Assert.Contains("cannot be undone", confirm.Prompt.Consequence, StringComparison.Ordinal);
        Assert.Equal(new WorktreeForceRemoveConfirmed(Tab, Linked), confirm.OnAccepted);
        Assert.Equal(
            $"{Linked} has changes and the screen is too small to ask: nothing was removed",
            confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenTheSecondConfirm_WhenAnswered_ThenTheForcedRemovalStarts()
    {
        var update = Reduce(new WorktreeForceRemoveConfirmed(Tab, Linked), Context());

        Assert.True(Assert.IsType<RemoveWorktreeEffect>(Assert.Single(update.Effects)).Force);
    }
}
