using Gitunia.Core.Stash;
using Gitunia.Git.Stash;
using Xunit;

namespace Gitunia.Core.Tests.Stash;

public class StashViewRulesTests
{
    private static readonly StashEntry Shown = new(0, "stash@{0}", "abc123", "wip");

    [Fact]
    public void GivenTheStashIsInTheStack_WhenReconciling_ThenNothingChanges()
    {
        var change = StashViewRules.Reconcile(Shown, [Shown]);

        Assert.Equal(StashViewChangeKind.Unchanged, change.Kind);
    }

    [Fact]
    public void GivenTheStashLeftTheStack_WhenReconciling_ThenItIsGoneAndNoOtherStashIsOffered()
    {
        var other = new StashEntry(0, "stash@{0}", "feedface", "newer");

        var change = StashViewRules.Reconcile(Shown, [other]);

        Assert.Equal(StashViewChangeKind.Gone, change.Kind);
        Assert.Null(change.Current);
    }

    [Fact]
    public void GivenANewStashPushedItDown_WhenReconciling_ThenTheSameStashIsKeptUnderItsNewName()
    {
        var newer = new StashEntry(0, "stash@{0}", "feedface", "newer");
        var moved = Shown with { Index = 1, Name = "stash@{1}" };

        var change = StashViewRules.Reconcile(Shown, [newer, moved]);

        Assert.Equal(StashViewChangeKind.Renamed, change.Kind);
        Assert.Equal(moved, change.Current);
    }

    [Fact]
    public void GivenAStatusThatNamesTheStash_WhenAskingToWarn_ThenTheDropMessageIsKept()
    {
        Assert.False(StashViewRules.ShouldWarnGone(Shown, "Dropped stash@{0} (abc); undo: git stash store"));
        Assert.True(StashViewRules.ShouldWarnGone(Shown, "Something else"));
        Assert.True(StashViewRules.ShouldWarnGone(Shown, null));
    }

    [Fact]
    public void GivenACommit_WhenFinding_ThenOnlyThatCommitMatches()
    {
        Assert.Equal(Shown, StashViewRules.Find([Shown], "abc123"));
        Assert.Null(StashViewRules.Find([Shown], "nope"));
        Assert.Null(StashViewRules.Find([Shown], null));
    }
}
