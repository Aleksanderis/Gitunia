using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;
using Xunit;
using static Gitunia.Core.Tests.Branches.BranchTestData;

namespace Gitunia.Core.Tests.Branches;

public class BranchSwitchReducerTests
{
    public static TheoryData<RepositoryOperation> Operations =>
    [
        RepositoryOperation.Merge,
        RepositoryOperation.Rebase,
        RepositoryOperation.CherryPick,
        RepositoryOperation.Revert,
    ];

    [Theory]
    [MemberData(nameof(Operations))]
    public void GivenAnOperationInProgress_WhenSwitchingFromTheSidebar_ThenItIsRefusedAndNothingRuns(
        RepositoryOperation kind)
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), MainAndTopic(), operation: Stopped(kind));

        Assert.Contains("finish or abort the", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOperationStartingWhileThePickerIsOpen_WhenChoosing_ThenNothingIsCheckedOut()
    {
        var choice = new BranchSwitchChosen(Tab, "refs/heads/topic");

        var effects = Reduce(choice, MainAndTopic(), operation: Stopped(RepositoryOperation.Merge));

        Assert.Contains("finish or abort the merge first", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARunningBranchOperation_WhenSwitching_ThenItIsRefusedAsBusy()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), MainAndTopic(), busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(effects));
    }

    [Fact]
    public void GivenASidebarLocalBranchRow_WhenSwitching_ThenItIsCheckedOutAtOnceAndTheCursorFollows()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), MainAndTopic());

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.LocalBranch, effect.TargetKind);
        Assert.Equal("topic", effect.Target);
        Assert.Equal(Topic, effect.Follow);
    }

    [Fact]
    public void GivenTheCurrentBranchRow_WhenSwitching_ThenTheWarningSaysAlreadyThereAndNoPromptOpens()
    {
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "main", Main);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), MainAndTopic());

        Assert.Equal("Already on main", Warning(effects));
    }

    [Fact]
    public void GivenABranchRowHeldByAnotherWorktree_WhenSwitching_ThenItIsRefusedWithoutAPrompt()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic, checkedOutIn: "/work/wt"));
        var target = new BranchTarget(SidebarRowKind.LocalBranch, "topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), refs);

        Assert.Equal("topic is checked out in /work/wt", Warning(effects));
    }

    [Fact]
    public void GivenASidebarRemoteBranchRowWithoutTwin_WhenSwitching_ThenATrackingBranchIsCreatedAtOnce()
    {
        var refs = Refs(Local("main", Main, current: true), Remote("origin/topic", Topic));
        var target = new BranchTarget(SidebarRowKind.RemoteBranch, "origin/topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), refs);

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.RemoteBranch, effect.TargetKind);
        Assert.Equal("origin/topic", effect.Target);
        Assert.Equal(Topic, effect.Follow);
    }

    [Fact]
    public void GivenASidebarRemoteBranchRowWithALocalTwin_WhenSwitching_ThenTheLocalBranchIsCheckedOut()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic), Remote("origin/topic", Topic));
        var target = new BranchTarget(SidebarRowKind.RemoteBranch, "origin/topic", Topic);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), refs);

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.LocalBranch, effect.TargetKind);
        Assert.Equal("topic", effect.Target);
    }

    [Fact]
    public void GivenATagRow_WhenSwitching_ThenThePickerOpensWithEveryBranch()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic), Tag("v1", Main));
        var target = new BranchTarget(SidebarRowKind.Tag, "v1", Main);

        var effects = Reduce(new BranchSwitchRequested(Tab, target), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Switch branch", request.Title);
        Assert.Equal("Branch to check out", request.Context);
        Assert.Equal("switch", request.AcceptLabel);
        Assert.Equal("No matching branch", request.NoMatch);
        Assert.Equal(Tab, request.Owner);
        Assert.Equal(["main", "topic"], request.Choices!.Value.Select(choice => choice.Text));
    }

    [Fact]
    public void GivenAGraphCommitWithOneBranchLabel_WhenSwitching_ThenThatBranchIsCheckedOutAtOnce()
    {
        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Topic)), MainAndTopic());

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal("topic", effect.Target);
    }

    [Fact]
    public void GivenAGraphCommitWithSeveralBranchLabels_WhenSwitching_ThenThePickerListsOnlyThoseAndDetached()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("topic", Topic),
            Local("old", Behind),
            Local("older", Behind));

        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Behind)), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(["old", "older", "detached at 3333333"], request.Choices!.Value.Select(choice => choice.Text));
        Assert.Equal(Behind, request.Choices.Value[2].Value);
    }

    [Fact]
    public void GivenAGraphCommitWithoutBranchLabels_WhenSwitching_ThenThePickerListsAllBranchesAndDetached()
    {
        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Behind)), MainAndTopic());

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(["main", "topic", "detached at 3333333"], request.Choices!.Value.Select(choice => choice.Text));
    }

    [Fact]
    public void GivenAGraphCommitWhoseOnlyLabelIsTheCurrentBranch_WhenSwitching_ThenTheWarningSaysAlreadyThere()
    {
        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Main)), MainAndTopic());

        Assert.Equal("Already on main", Warning(effects));
    }

    [Fact]
    public void GivenAGraphCommitWhoseOnlyLabelIsHeldByAnotherWorktree_WhenSwitching_ThenItIsRefused()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic, checkedOutIn: "/work/wt"));

        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Topic)), refs);

        Assert.Contains("is checked out in", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAGraphCommitWithOnlyARemoteLabel_WhenSwitching_ThenATrackingBranchIsCreatedAtOnce()
    {
        var refs = Refs(Local("main", Main, current: true), Remote("origin/topic", Topic));

        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, Topic)), refs);

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.RemoteBranch, effect.TargetKind);
    }

    [Fact]
    public void GivenARemoteBranchWithoutLocalTwin_WhenOpeningThePicker_ThenTheListShowsTheLocalName()
    {
        var refs = Refs(Local("main", Main, current: true), Remote("origin/topic", Topic));

        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, null)), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(["main", "origin/topic -> local topic"], request.Choices!.Value.Select(choice => choice.Text));
        Assert.Equal("refs/remotes/origin/topic", request.Choices.Value[1].Value);
    }

    [Fact]
    public void GivenMoreBranchesThanFitTheList_WhenOpeningThePicker_ThenEveryBranchIsACandidate()
    {
        var branches = Enumerable.Range(0, 12).Select(number => Local($"b{number:D2}", Main)).ToArray();
        var refs = Refs([Local("main", Main, current: true), Local("topic", Topic), .. branches]);

        var effects = Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, null)), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal(14, request.Choices!.Value.Length);
    }

    [Fact]
    public void GivenALocalChoice_WhenChosen_ThenTheBranchIsCheckedOut()
    {
        var effects = Reduce(new BranchSwitchChosen(Tab, "refs/heads/topic"), MainAndTopic());

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.LocalBranch, effect.TargetKind);
        Assert.Equal("topic", effect.Target);
        Assert.Equal(Topic, effect.Follow);
    }

    [Fact]
    public void GivenARemoteChoice_WhenChosen_ThenATrackingBranchIsCreated()
    {
        var refs = Refs(Local("main", Main, current: true), Remote("origin/topic", Topic));

        var effects = Reduce(new BranchSwitchChosen(Tab, "refs/remotes/origin/topic"), refs);

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.RemoteBranch, effect.TargetKind);
        Assert.Equal("origin/topic", effect.Target);
    }

    [Fact]
    public void GivenTheDetachedChoice_WhenChosen_ThenHeadIsDetachedAtTheCommitAndTheCursorFollows()
    {
        var effects = Reduce(new BranchSwitchChosen(Tab, Behind), MainAndTopic());

        var effect = Assert.IsType<SwitchBranchEffect>(Assert.Single(effects));
        Assert.Equal(SwitchTargetKind.DetachedCommit, effect.TargetKind);
        Assert.Equal(Behind, effect.Target);
        Assert.Equal(Behind, effect.Follow);
    }

    [Fact]
    public void GivenTheCurrentBranchChosen_WhenChosen_ThenTheWarningSaysAlreadyThere()
    {
        var effects = Reduce(new BranchSwitchChosen(Tab, "refs/heads/main"), MainAndTopic());

        Assert.Equal("Already on main", Warning(effects));
    }

    [Fact]
    public void GivenABranchChosenThatAnotherWorktreeHolds_WhenChosen_ThenItIsRefused()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic, checkedOutIn: "/work/wt"));

        var effects = Reduce(new BranchSwitchChosen(Tab, "refs/heads/topic"), refs);

        Assert.Equal("topic is checked out in /work/wt", Warning(effects));
    }

    [Fact]
    public void GivenABranchThatIsGone_WhenChosen_ThenNothingMatches()
    {
        var effects = Reduce(new BranchSwitchChosen(Tab, "refs/heads/gone"), MainAndTopic());

        Assert.Equal("No matching branch", Warning(effects));
    }

    [Fact]
    public void GivenARemoteRow_WhenFilteringForTheLocalSuffix_ThenOnlyTheNameIsMatched()
    {
        var refs = Refs(Local("main", Main, current: true), Remote("origin/topic", Topic));
        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(
            Reduce(new BranchSwitchRequested(Tab, new BranchTarget(null, null, null)), refs))).Request;

        var picker = PromptPicker.Of(request.Choices!.Value, request.NoMatch, "local");

        Assert.Empty(picker.Candidates);
    }

    private static OperationInfo Stopped(RepositoryOperation kind) =>
        new(kind, null, null, null, null, null, null, null);
}
