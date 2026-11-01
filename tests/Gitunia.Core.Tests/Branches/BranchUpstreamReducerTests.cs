using System.Collections.Immutable;
using Gitunia.Core.Branches;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Git.Refs.Models;
using Xunit;
using static Gitunia.Core.Tests.Branches.BranchTestData;

namespace Gitunia.Core.Tests.Branches;

public class BranchUpstreamReducerTests
{
    private const string TopicUpstream = "refs/remotes/origin/topic";

    [Fact]
    public void GivenALocalBranchWithoutUpstream_WhenSettingOne_ThenThePickerListsTheRemoteBranches()
    {
        var refs = Refs(Local("main", Main, current: true), Local("lonely", Topic), Remote("origin/main", Main));

        var effects = Reduce(new BranchSetUpstreamRequested(Tab, Lonely()), refs);

        var request = Assert.IsType<ShowPromptEffect>(Assert.Single(effects)).Request;
        Assert.Equal("Set upstream", request.Title);
        Assert.Equal("Upstream of lonely", request.Context);
        Assert.Equal("set", request.AcceptLabel);
        Assert.Equal("No matching remote branch", request.NoMatch);
        Assert.Equal(new BranchUpstreamAnswer(Tab, "lonely"), request.OnAccepted);
        var choice = Assert.Single(request.Choices!.Value);
        Assert.Equal(new PromptChoice("origin/main", "origin/main"), choice);
    }

    [Fact]
    public void GivenARepositoryWithoutRemoteBranches_WhenSettingUpstream_ThenNoPromptOpens()
    {
        var refs = Refs(Local("main", Main, current: true), Local("lonely", Topic));

        var effects = Reduce(new BranchSetUpstreamRequested(Tab, Lonely()), refs);

        Assert.Equal("There is no remote branch to track: add or fetch a remote first", Warning(effects));
    }

    [Fact]
    public void GivenARemoteBranchOnlyWithASymbolicHead_WhenSettingUpstream_ThenNoPromptOpens()
    {
        var head = Remote("origin/HEAD", Main) with { SymbolicTarget = "refs/remotes/origin/main" };
        var refs = Refs(Local("main", Main, current: true), Local("lonely", Topic), head);

        var effects = Reduce(new BranchSetUpstreamRequested(Tab, Lonely()), refs);

        Assert.Contains("no remote branch", Warning(effects), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenARemoteBranchPicked_WhenChosen_ThenTheUpstreamIsSet()
    {
        var effects = Reduce(new BranchUpstreamChosen(Tab, "lonely", "origin/main"), MainAndTopic());

        var set = Assert.IsType<SetUpstreamEffect>(Assert.Single(effects));
        Assert.Equal("lonely", set.Name);
        Assert.Equal("origin/main", set.Upstream);
        Assert.Null(set.Follow);
    }

    [Fact]
    public void GivenARunningOperation_WhenSettingOrChoosingAnUpstream_ThenItIsRefusedAsBusy()
    {
        var refs = Refs(Local("lonely", Topic), Remote("origin/main", Main));

        var request = Reduce(new BranchSetUpstreamRequested(Tab, Lonely()), refs, busy: true);
        var chosen = Reduce(new BranchUpstreamChosen(Tab, "lonely", "origin/main"), refs, busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(request));
        Assert.Equal(BranchRefusals.BusyNotice, Warning(chosen));
    }

    [Fact]
    public void GivenATrackingBranch_WhenUnsetting_ThenTheEffectCarriesTheShownUpstreamForTheRestoreHint()
    {
        var refs = Refs(Local("main", Main, current: true), Local("topic", Topic, upstream: TopicUpstream));

        var effects = Reduce(new BranchUnsetUpstreamRequested(Tab, Row("topic")), refs);

        var unset = Assert.IsType<UnsetUpstreamEffect>(Assert.Single(effects));
        Assert.Equal("topic", unset.Name);
        Assert.Equal("origin/topic", unset.Upstream);
    }

    [Fact]
    public void GivenABranchWithoutUpstream_WhenUnsetting_ThenGitIsNotRun()
    {
        var effects = Reduce(new BranchUnsetUpstreamRequested(Tab, Lonely()), Refs(Local("lonely", Topic)));

        Assert.Equal("lonely has no upstream", Warning(effects));
    }

    [Fact]
    public void GivenABranchBehindItsUpstream_WhenFastForwarding_ThenTheEffectCarriesBothTips()
    {
        var refs = BehindRefs();

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        var fastForward = Assert.IsType<FastForwardBranchEffect>(Assert.Single(effects));
        Assert.Equal("topic", fastForward.Name);
        Assert.Equal(TopicUpstream, fastForward.UpstreamFullName);
        Assert.Equal(Topic, fastForward.OldHash);
        Assert.Equal(Behind, fastForward.NewHash);
    }

    [Fact]
    public void GivenAGraphCommitWithTheBehindBranch_WhenFastForwarding_ThenItMoves()
    {
        var effects = Reduce(new BranchFastForwardRequested(Tab, new BranchTarget(null, null, Topic)), BehindRefs());

        Assert.IsType<FastForwardBranchEffect>(Assert.Single(effects));
    }

    [Fact]
    public void GivenADivergedBranch_WhenFastForwarding_ThenItIsRefusedWithoutRunningGit()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("topic", Topic, upstream: TopicUpstream, ahead: 1),
            Remote("origin/topic", Behind));

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        Assert.Equal("topic has commits its upstream lacks (diverged): nothing changed", Warning(effects));
    }

    [Fact]
    public void GivenAnUpToDateBranch_WhenFastForwarding_ThenItSaysSo()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("topic", Topic, upstream: TopicUpstream),
            Remote("origin/topic", Topic));

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        Assert.Equal("topic is up to date with its upstream", Warning(effects));
    }

    [Fact]
    public void GivenTheCurrentBranch_WhenFastForwarding_ThenThePullKeyIsPointedTo()
    {
        var refs = Refs(Local("topic", Topic, current: true, upstream: TopicUpstream), Remote("origin/topic", Behind));

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        Assert.Equal("topic is checked out: pull it with <space>p instead", Warning(effects));
    }

    [Fact]
    public void GivenABranchInAnotherWorktree_WhenFastForwarding_ThenItIsRefusedNamingTheWorktree()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("topic", Topic, checkedOutIn: "/work/wt", upstream: TopicUpstream),
            Remote("origin/topic", Behind));

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        Assert.Equal("topic is checked out in /work/wt", Warning(effects));
    }

    [Fact]
    public void GivenABranchWithoutUpstream_WhenFastForwarding_ThenItIsRefused()
    {
        var effects = Reduce(new BranchFastForwardRequested(Tab, Lonely()), Refs(Local("lonely", Topic)));

        Assert.Equal("lonely has no upstream", Warning(effects));
    }

    [Fact]
    public void GivenAGoneUpstream_WhenFastForwarding_ThenNothingChanges()
    {
        var refs = Refs(
            Local("main", Main, current: true),
            Local("topic", Topic, upstream: TopicUpstream, upstreamGone: true));

        var effects = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs);

        Assert.Equal("The upstream of topic is gone: nothing changed", Warning(effects));
    }

    [Fact]
    public void GivenARunningOperation_WhenFastForwardingOrUnsetting_ThenItIsRefusedAsBusy()
    {
        var refs = BehindRefs();

        var fastForward = Reduce(new BranchFastForwardRequested(Tab, Row("topic")), refs, busy: true);
        var unset = Reduce(new BranchUnsetUpstreamRequested(Tab, Row("topic")), refs, busy: true);

        Assert.Equal(BranchRefusals.BusyNotice, Warning(fastForward));
        Assert.Equal(BranchRefusals.BusyNotice, Warning(unset));
    }

    private static ImmutableArray<GitRef> BehindRefs() => Refs(
        Local("main", Main, current: true),
        Local("topic", Topic, upstream: TopicUpstream),
        Remote("origin/topic", Behind));

    private static BranchTarget Lonely() => Row("lonely");

    private static BranchTarget Row(string name) => new(SidebarRowKind.LocalBranch, name, null);
}
