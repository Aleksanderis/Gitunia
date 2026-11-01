using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Xunit;
using static Gitunia.Core.Tests.Branches.BranchTestData;

namespace Gitunia.Core.Tests.Branches;

public class BranchDeleteReducerTests
{
    [Fact]
    public void GivenAMergedBranchRow_WhenDeleting_ThenOneDialogNamesItAndTheUndoHint()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchDeleteRequested(Tab, target), MainAndTopic());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Delete branch", confirm.Prompt.Title);
        Assert.Contains("topic", confirm.Prompt.Lines[0], StringComparison.Ordinal);
        Assert.Equal($"Undo: git branch topic {Topic}", confirm.Prompt.Consequence);
        Assert.Equal(new BranchDeleteConfirmed(Tab, "topic", Topic, null), confirm.OnAccepted);
        Assert.Contains("nothing was deleted", confirm.UnavailableNotice, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheConfirmedDelete_WhenReduced_ThenTheDeleteRunsWithoutForceAndTheCursorFollowsHead()
    {
        var confirmed = new BranchDeleteConfirmed(Tab, "topic", Topic, "refs/remotes/origin/topic");

        var effects = Reduce(confirmed, MainAndTopic());

        var delete = Assert.IsType<DeleteBranchEffect>(Assert.Single(effects));
        Assert.False(delete.Force);
        Assert.Equal("topic", delete.Name);
        Assert.Equal("refs/remotes/origin/topic", delete.UpstreamFullName);
        Assert.Equal(Main, delete.Follow);
    }

    [Fact]
    public void GivenARunningOperation_WhenTheDeleteIsConfirmed_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new BranchDeleteConfirmed(Tab, "topic", Topic, null), MainAndTopic(), busy: true);

        Assert.Equal($"{BranchRefusals.BusyNotice}; nothing was deleted", Warning(effects));
    }

    [Fact]
    public void GivenAGraphCommitWithTwoBranches_WhenDeleting_ThenAListOpensWithBoth()
    {
        var refs = Refs(Local("main", Main, current: true), Local("second", Main), Local("topic", Topic));

        var effects = Reduce(new BranchDeleteRequested(Tab, new BranchTarget(null, null, Main)), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Delete branch", request.Title);
        Assert.Equal("Branch to delete at 1111111", request.Context);
        Assert.Equal("choose", request.AcceptLabel);
        Assert.True(request.ListOnly);
        Assert.Equal(["main", "second"], request.Choices!.Value.Select(choice => choice.Value));
        Assert.Equal(new BranchDeleteAnswer(Tab), request.OnAccepted);
        Assert.Equal(Tab, request.Owner);
    }

    [Fact]
    public void GivenAFolderRow_WhenDeleting_ThenOnlyItsBranchesAreListed()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("feature/a", Main),
            Local("feature/b", Main),
            Local("other", Main));

        var folder = new BranchTarget(SidebarRowKind.Folder, "feature", null);

        var effects = Reduce(new BranchDeleteRequested(Tab, folder), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Branch to delete at feature/", request.Context);
        Assert.Equal(["feature/a", "feature/b"], request.Choices!.Value.Select(choice => choice.Value));
    }

    [Fact]
    public void GivenAFolderWithOneBranch_WhenDeleting_ThenTheConfirmOpensAtOnce()
    {
        var refs = Refs(Local("main", Main, current: true), Local("feature/a", Topic));

        var folder = new BranchTarget(SidebarRowKind.Folder, "feature", null);

        var effects = Reduce(new BranchDeleteRequested(Tab, folder), refs);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Contains("feature/a", confirm.Prompt.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFolderWithoutBranches_WhenDeleting_ThenTheWarningNamesTheFolder()
    {
        var effects = Reduce(
            new BranchDeleteRequested(Tab, new BranchTarget(SidebarRowKind.Folder, "feature", null)),
            MainAndTopic());

        Assert.Equal("No local branch in feature/", Warning(effects));
    }

    [Fact]
    public void GivenTheListChoice_WhenTheBranchIsUnmergedOrNot_ThenTheConfirmNamesTheChosenBranch()
    {
        var effects = Reduce(new BranchDeleteChosen(Tab, "topic"), MainAndTopic());

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal(new BranchDeleteConfirmed(Tab, "topic", Topic, null), confirm.OnAccepted);
    }

    [Fact]
    public void GivenTheListChoiceOnTheCurrentBranch_WhenChosen_ThenItIsRefusedWithoutADialog()
    {
        var effects = Reduce(new BranchDeleteChosen(Tab, "main"), MainAndTopic());

        Assert.Equal("main is checked out: switch to another branch first", Warning(effects));
    }

    [Fact]
    public void GivenAListChoiceThatIsGone_WhenChosen_ThenNothingIsDeleted()
    {
        var effects = Reduce(new BranchDeleteChosen(Tab, "gone"), MainAndTopic());

        Assert.Equal("That branch is gone: nothing was deleted", Warning(effects));
    }

    [Fact]
    public void GivenTheCurrentBranch_WhenDeleting_ThenItIsRefusedWithoutADialog()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "main", Main);

        var effects = Reduce(new BranchDeleteRequested(Tab, target), MainAndTopic());

        Assert.Equal("main is checked out: switch to another branch first", Warning(effects));
    }

    [Fact]
    public void GivenABranchInAnotherWorktree_WhenDeleting_ThenItIsRefusedWithoutADialog()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic, checkedOutIn: "/work/wt"));
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchDeleteRequested(Tab, target), refs);

        Assert.Equal("topic is checked out in /work/wt", Warning(effects));
    }

    [Fact]
    public void GivenARunningOperation_WhenDeleting_ThenItIsRefusedAsBusy()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchDeleteRequested(Tab, target), MainAndTopic(), busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenAnUnmergedBranch_WhenGitSaysSo_ThenASecondDialogNamesTheRiskAndTheUndo()
    {
        var effects = Reduce(new BranchDeleteNeedsForce(Tab, "wip", Topic), MainAndTopic(), busy: true);

        var confirm = Assert.IsType<ShowConfirmEffect>(Assert.Single(effects));
        Assert.Equal("Delete unmerged branch", confirm.Prompt.Title);
        Assert.Contains("unreachable", string.Join(' ', confirm.Prompt.Lines), StringComparison.Ordinal);
        Assert.Equal($"Undo: git branch wip {Topic}", confirm.Prompt.Consequence);
        Assert.Equal("Delete anyway", confirm.Prompt.YesLabel);
        Assert.Equal(new BranchForceDeleteConfirmed(Tab, "wip", Topic), confirm.OnAccepted);
        Assert.Equal(
            "wip is unmerged and the screen is too small to ask: nothing was deleted",
            confirm.UnavailableNotice);
    }

    [Fact]
    public void GivenTheSecondAnswerYes_WhenReduced_ThenTheForcedDeleteRuns()
    {
        var effects = Reduce(new BranchForceDeleteConfirmed(Tab, "wip", Topic), MainAndTopic());

        var delete = Assert.IsType<DeleteBranchEffect>(Assert.Single(effects));
        Assert.True(delete.Force);
        Assert.Equal(Main, delete.Follow);
    }

    [Fact]
    public void GivenARunningOperation_WhenTheForcedDeleteIsConfirmed_ThenItIsRefusedAsBusy()
    {
        var effects = Reduce(new BranchForceDeleteConfirmed(Tab, "wip", Topic), MainAndTopic(), busy: true);

        Assert.Equal($"{BranchRefusals.BusyNotice}; nothing was deleted", Warning(effects));
    }
}
