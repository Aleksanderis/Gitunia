using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class BranchColorsTests
{
    [Fact]
    public void GivenLocalAndRemoteBranchOfTheSameName_WhenNamingTheBranch_ThenBothGiveTheSameNameAndSlot()
    {
        var local = BranchColors.PrimaryName([Branch("main")]);
        var remote = BranchColors.PrimaryName([Remote("origin/main")]);

        Assert.Equal("main", local);
        Assert.Equal(local, remote);
        Assert.Equal(BranchColors.PreferredSlot(local!), BranchColors.PreferredSlot(remote!));
    }

    [Fact]
    public void GivenAnotherRemoteName_WhenNamingTheBranch_ThenOnlyTheFirstSegmentIsDropped()
    {
        Assert.Equal("feature/x", BranchColors.PrimaryName([Remote("upstream/feature/x")]));
    }

    [Fact]
    public void GivenLocalAndRemoteOnOneCommit_WhenNamingTheBranch_ThenTheLocalBranchWins()
    {
        Assert.Equal("topic", BranchColors.PrimaryName([Remote("origin/other"), Branch("topic")]));
    }

    [Fact]
    public void GivenOnlyTheSymbolicRemoteHead_WhenNamingTheBranch_ThenThereIsNone()
    {
        Assert.Null(BranchColors.PrimaryName([Remote("origin/HEAD")]));
    }

    [Fact]
    public void GivenTagsAndDetachedHead_WhenNamingTheBranch_ThenThereIsNone()
    {
        var tag = new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false);
        var head = new CommitDecoration("HEAD", "HEAD", RefKind.Head, true);

        Assert.Null(BranchColors.PrimaryName([tag, head]));
        Assert.Null(BranchColors.PrimaryName([]));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("feature/long-name")]
    [InlineData("")]
    [InlineData("ünï")]
    public void GivenAName_WhenChoosingTheSlot_ThenItIsInRangeAndTheSameEveryTime(string name)
    {
        var slot = BranchColors.PreferredSlot(name);

        Assert.InRange(slot, 0, BranchColors.SlotCount - 1);
        Assert.Equal(slot, BranchColors.PreferredSlot(name));
    }

    [Fact]
    public void GivenDecorationsOfSeveralCommits_WhenListingTips_ThenOnlyBranchCommitsAreIn()
    {
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>
        {
            [GraphTestData.Id(1)] = [Branch("main")],
            [GraphTestData.Id(2)] = [new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false)],
            [GraphTestData.Id(3)] = [Remote("origin/topic")],
        };

        var tips = BranchColors.TipsOf(decorations);

        Assert.Equal(2, tips.Count);
        Assert.Equal("main", tips[GraphTestData.Id(1)]);
        Assert.Equal("topic", tips[GraphTestData.Id(3)]);
    }

    private static CommitDecoration Branch(string name) =>
        new($"refs/heads/{name}", name, RefKind.LocalBranch, false);

    private static CommitDecoration Remote(string name) =>
        new($"refs/remotes/{name}", name, RefKind.RemoteBranch, false);
}
