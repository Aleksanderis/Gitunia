using Gitunia.Core.Branches;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Core.Tests.Branches;

public class SwitchCandidatesTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void GivenARemoteBranchWithALocalTwin_WhenBuilding_ThenOnlyTheLocalOneIsListed()
    {
        var refs = new[]
        {
            Branch("topic", RefKind.LocalBranch),
            Branch("origin/topic", RefKind.RemoteBranch),
            Branch("origin/other", RefKind.RemoteBranch),
        };

        var labels = SwitchCandidates.Build(refs, null, string.Empty).Select(candidate => candidate.Label);

        Assert.Equal(["topic", "origin/other"], labels);
    }

    [Fact]
    public void GivenARemoteHeadAlias_WhenBuilding_ThenItIsLeftOut()
    {
        var alias = Branch("origin/HEAD", RefKind.RemoteBranch) with { SymbolicTarget = "refs/remotes/origin/main" };

        var candidates = SwitchCandidates.Build([alias], null, string.Empty);

        Assert.Empty(candidates);
    }

    [Fact]
    public void GivenABranchInAnotherWorktree_WhenBuilding_ThenItCarriesThePathButTheCurrentOneDoesNot()
    {
        var refs = new[]
        {
            Branch("main", RefKind.LocalBranch, isCurrent: true, checkedOutIn: "/repo"),
            Branch("topic", RefKind.LocalBranch, checkedOutIn: "/other"),
        };

        var candidates = SwitchCandidates.Build(refs, null, string.Empty);

        Assert.True(candidates[0].IsCurrent);
        Assert.Null(candidates[0].CheckedOutIn);
        Assert.Equal("/other", candidates[1].CheckedOutIn);
    }

    [Fact]
    public void GivenAGraphCommit_WhenBuilding_ThenADetachedEntryComesLast()
    {
        var candidates = SwitchCandidates.Build([Branch("main", RefKind.LocalBranch)], Hash, string.Empty);

        Assert.Equal(SwitchTargetKind.DetachedCommit, candidates[^1].Kind);
        Assert.Equal("detached at 0123456", candidates[^1].Label);
        Assert.Equal(Hash, candidates[^1].Target);
    }

    [Fact]
    public void GivenAQuery_WhenBuilding_ThenOnlyFuzzyMatchesRemainBestFirst()
    {
        var refs = new[]
        {
            Branch("feature/rel", RefKind.LocalBranch),
            Branch("release/1", RefKind.LocalBranch),
            Branch("main", RefKind.LocalBranch),
        };

        var labels = SwitchCandidates.Build(refs, null, "rel").Select(candidate => candidate.Label).ToList();

        Assert.Equal("release/1", labels[0]);
        Assert.DoesNotContain("main", labels);
        Assert.Equal(2, labels.Count);
    }

    [Theory]
    [InlineData("origin/topic", "topic")]
    [InlineData("origin/feature/x", "feature/x")]
    [InlineData("plain", "plain")]
    public void GivenARemoteBranch_WhenNamingItsLocalTwin_ThenTheRemotePrefixIsDropped(string remote, string expected) =>
        Assert.Equal(expected, SwitchCandidates.LocalNameOf(remote));

    private static GitRef Branch(string name, RefKind kind, bool isCurrent = false, string? checkedOutIn = null) =>
        new(
            $"refs/{(kind == RefKind.LocalBranch ? "heads" : "remotes")}/{name}",
            name,
            kind,
            Hash,
            Hash,
            null,
            null,
            null,
            false,
            isCurrent,
            checkedOutIn,
            null);
}
