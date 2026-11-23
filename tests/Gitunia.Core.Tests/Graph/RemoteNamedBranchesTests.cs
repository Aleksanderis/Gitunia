using Gitunia.Core.Graph;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class RemoteNamedBranchesTests
{
    [Fact]
    public void GivenTwoRemotesOnDifferentCommits_WhenComputing_ThenTheBranchIsNamed()
    {
        var refs = new[]
        {
            Ref("main", RefKind.LocalBranch, "a"),
            Ref("origin/main", RefKind.RemoteBranch, "a"),
            Ref("fork/main", RefKind.RemoteBranch, "b"),
        };

        Assert.Equal(["main"], RemoteNamedBranches.Of(refs));
    }

    [Fact]
    public void GivenTwoRemotesOnTheSameCommit_WhenComputing_ThenNothingIsNamed()
    {
        var refs = new[]
        {
            Ref("main", RefKind.LocalBranch, "a"),
            Ref("origin/main", RefKind.RemoteBranch, "a"),
            Ref("fork/main", RefKind.RemoteBranch, "a"),
        };

        Assert.Empty(RemoteNamedBranches.Of(refs));
    }

    [Fact]
    public void GivenOneRemoteBehindItsLocalBranch_WhenComputing_ThenNothingIsNamed()
    {
        var refs = new[]
        {
            Ref("main", RefKind.LocalBranch, "b"),
            Ref("origin/main", RefKind.RemoteBranch, "a"),
            Ref("origin/HEAD", RefKind.RemoteBranch, "c"),
        };

        Assert.Empty(RemoteNamedBranches.Of(refs));
    }

    private static GitRef Ref(string name, RefKind kind, string commit) =>
        new(name, name, kind, commit, commit, null, null, null, false, false, null, null);
}
