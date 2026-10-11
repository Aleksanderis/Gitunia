using Xunit;

namespace Gitunia.Git.Tests;

public class RefParserTests
{
    private const string HashOne = "1111111111111111111111111111111111111111";
    private const string HashTwo = "2222222222222222222222222222222222222222";
    private const string HashThree = "3333333333333333333333333333333333333333";

    private static string Line(string fullName, string objectHash, string peeled = "", string upstream = "", string track = "", string head = " ", string worktree = "", string symref = "") =>
        string.Join('\0', fullName, objectHash, peeled, upstream, track, head, worktree, symref) + "\n";

    [Fact]
    public void GivenCurrentBranchWithUpstream_WhenParsing_ThenAheadBehindAndWorktreeAreSet()
    {
        string output = Line("refs/heads/main", HashOne, upstream: "refs/remotes/origin/main", track: "ahead 2, behind 1", head: "*", worktree: "/work/repo");

        GitRef reference = Assert.Single(RefParser.Parse(output).Items);

        Assert.Equal("main", reference.Name);
        Assert.Equal(RefKind.LocalBranch, reference.Kind);
        Assert.Equal("refs/remotes/origin/main", reference.UpstreamFullName);
        Assert.Equal(2, reference.Ahead);
        Assert.Equal(1, reference.Behind);
        Assert.True(reference.IsCurrent);
        Assert.Equal("/work/repo", reference.CheckedOutIn);
    }

    [Theory]
    [InlineData("ahead 3", 3, 0)]
    [InlineData("behind 4", 0, 4)]
    [InlineData("", 0, 0)]
    public void GivenUpstreamTrackingText_WhenParsing_ThenCountsMatch(string track, int ahead, int behind)
    {
        string output = Line("refs/heads/topic", HashOne, upstream: "refs/remotes/origin/topic", track: track);

        GitRef reference = Assert.Single(RefParser.Parse(output).Items);

        Assert.Equal(ahead, reference.Ahead);
        Assert.Equal(behind, reference.Behind);
        Assert.False(reference.IsUpstreamGone);
    }

    [Fact]
    public void GivenGoneUpstream_WhenParsing_ThenFlaggedWithoutCounts()
    {
        string output = Line("refs/heads/old", HashOne, upstream: "refs/remotes/origin/old", track: "gone");

        GitRef reference = Assert.Single(RefParser.Parse(output).Items);

        Assert.True(reference.IsUpstreamGone);
        Assert.Null(reference.Ahead);
        Assert.Null(reference.Behind);
    }

    [Fact]
    public void GivenBranchWithoutUpstream_WhenParsing_ThenNoCounts()
    {
        GitRef reference = Assert.Single(RefParser.Parse(Line("refs/heads/local", HashOne)).Items);

        Assert.Null(reference.UpstreamFullName);
        Assert.Null(reference.Ahead);
        Assert.False(reference.IsCurrent);
        Assert.Null(reference.CheckedOutIn);
    }

    [Fact]
    public void GivenAnnotatedTag_WhenParsing_ThenCommitIsThePeeledObject()
    {
        string output = Line("refs/tags/v1", HashOne, peeled: HashTwo) + Line("refs/tags/light", HashThree);

        IReadOnlyList<GitRef> tags = RefParser.Parse(output).Items;

        Assert.Equal(HashOne, tags[0].ObjectHash);
        Assert.Equal(HashTwo, tags[0].CommitHash);
        Assert.Equal(HashThree, tags[1].CommitHash);
        Assert.All(tags, tag => Assert.Equal(RefKind.Tag, tag.Kind));
    }

    [Fact]
    public void GivenRemoteBranchesAndSymbolicHead_WhenParsing_ThenShortNamesKeepRemotePrefix()
    {
        string output = Line("refs/remotes/origin/feature/x", HashOne) + Line("refs/remotes/origin/HEAD", HashOne, symref: "refs/remotes/origin/main");

        IReadOnlyList<GitRef> refs = RefParser.Parse(output).Items;

        Assert.Equal("origin/feature/x", refs[0].Name);
        Assert.Equal(RefKind.RemoteBranch, refs[0].Kind);
        Assert.Equal("refs/remotes/origin/main", refs[1].SymbolicTarget);
    }

    [Fact]
    public void GivenMalformedAndForeignRefs_WhenParsing_ThenSkippedWithWarnings()
    {
        string output = "refs/heads/broken\0onlytwo\n" + Line("refs/stash", HashOne) + Line("refs/heads/ok", HashOne) + Line("refs/heads/odd", HashOne, track: "sideways 3", upstream: "refs/remotes/o/odd");

        ParseResult<GitRef> result = RefParser.Parse(output);

        Assert.Equal(["ok", "odd"], result.Items.Select(reference => reference.Name));
        Assert.Equal(3, result.Warnings.Count);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParsing_ThenNothing()
    {
        ParseResult<GitRef> result = RefParser.Parse(string.Empty);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
    }
}
