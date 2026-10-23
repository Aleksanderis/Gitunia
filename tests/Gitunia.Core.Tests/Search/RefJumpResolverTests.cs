using System.Globalization;
using Gitunia.Core.Search;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Core.Tests.Search;

[Trait("Os", "Independent")]
public class RefJumpResolverTests
{
    private static GitRef Ref(string name, RefKind kind, int commit, bool isCurrent = false, string? symbolicTarget = null)
    {
        var hash = commit.ToString("x40", CultureInfo.InvariantCulture);
        var prefix = kind switch
        {
            RefKind.LocalBranch => "refs/heads/",
            RefKind.RemoteBranch => "refs/remotes/",
            _ => "refs/tags/",
        };
        return new GitRef(prefix + name, name, kind, hash, hash, null, null, null, false, isCurrent, null, symbolicTarget);
    }

    private static readonly GitRef[] Refs =
    [
        Ref("main", RefKind.LocalBranch, 1, isCurrent: true),
        Ref("feature/login-form", RefKind.LocalBranch, 2),
        Ref("origin/main", RefKind.RemoteBranch, 1),
        Ref("origin/HEAD", RefKind.RemoteBranch, 1, symbolicTarget: "refs/remotes/origin/main"),
        Ref("v1.0", RefKind.Tag, 3),
    ];

    [Fact]
    public void GivenEmptyText_WhenResolving_ThenAllRealRefsAreListedLocalBranchesFirst()
    {
        var state = RefJumpResolver.Resolve(string.Empty, Refs, SearchTestGraph.Numbered(3));

        Assert.Equal(["main", "feature/login-form", "origin/main", "v1.0"], state.Candidates.Select(candidate => candidate.Label));
        Assert.Equal("main", state.Selected?.Label);
    }

    [Fact]
    public void GivenFuzzyText_WhenResolving_ThenOnlySubsequenceMatchesRemainBestFirst()
    {
        var state = RefJumpResolver.Resolve("lgn", Refs, SearchTestGraph.Numbered(3));

        Assert.Equal(["feature/login-form"], state.Candidates.Select(candidate => candidate.Label));
    }

    [Fact]
    public void GivenExactName_WhenResolving_ThenTheTighterMatchRanksFirst()
    {
        var state = RefJumpResolver.Resolve("main", Refs, SearchTestGraph.Numbered(3));

        Assert.Equal(["main", "origin/main"], state.Candidates.Select(candidate => candidate.Label));
    }

    [Fact]
    public void GivenHashPrefix_WhenResolving_ThenTheLoadedCommitIsACandidate()
    {
        var graph = SearchTestGraph.Numbered(3);

        var state = RefJumpResolver.Resolve("0000000000", [], graph);

        Assert.Equal(3, state.Candidates.Length);
        Assert.Null(state.Candidates[0].Kind);
        Assert.StartsWith("0000000 Commit #1", state.Candidates[0].Label, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenShortHexText_WhenResolving_ThenNoCommitIsSearched()
    {
        var state = RefJumpResolver.Resolve("000", [], SearchTestGraph.Numbered(3));

        Assert.Empty(state.Candidates);
        Assert.Null(state.Selected);
    }

    [Fact]
    public void GivenCandidates_WhenMoving_ThenTheHighlightWrapsAround()
    {
        var state = RefJumpResolver.Resolve(string.Empty, Refs, SearchTestGraph.Numbered(3));

        Assert.Equal("v1.0", state.Move(-1).Selected?.Label);
        Assert.Equal("feature/login-form", state.Move(1).Selected?.Label);
        Assert.Equal("main", state.Move(4).Selected?.Label);
    }
}
