using Gitunia.Git.Worktrees;
using Xunit;

namespace Gitunia.Git.Tests;

public class WorktreeParserTests
{
    private const string Hash = "1111111111111111111111111111111111111111";

    [Fact]
    public void GivenMainAndLinkedWorktrees_WhenParsing_ThenFirstIsMain()
    {
        var output = Record("worktree /work/repo", "HEAD " + Hash, "branch refs/heads/main")
            + Record("worktree /work/wt with space", "HEAD " + Hash, "branch refs/heads/feature");

        var worktrees = WorktreeParser.Parse(output).Items;

        Assert.Equal(2, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.False(worktrees[1].IsMain);
        Assert.Equal("/work/wt with space", worktrees[1].Path);
        Assert.Equal("refs/heads/feature", worktrees[1].BranchFullName);
    }

    [Fact]
    public void GivenUnbornBranch_WhenParsing_ThenHeadHashIsNull()
    {
        var output = Record("worktree /work/repo", "HEAD 0000000000000000000000000000000000000000", "branch refs/heads/main");

        var worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.Null(worktree.HeadHash);
        Assert.Equal("refs/heads/main", worktree.BranchFullName);
    }

    [Fact]
    public void GivenDetachedHead_WhenParsing_ThenNoBranch()
    {
        var output = Record("worktree /work/repo", "HEAD " + Hash, "detached");

        var worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.True(worktree.IsDetached);
        Assert.Null(worktree.BranchFullName);
    }

    [Fact]
    public void GivenLockedWithAndWithoutReason_WhenParsing_ThenReasonIsKept()
    {
        var output = Record("worktree /a", "HEAD " + Hash, "branch refs/heads/a", "locked on a usb stick")
            + Record("worktree /b", "HEAD " + Hash, "branch refs/heads/b", "locked");

        var worktrees = WorktreeParser.Parse(output).Items;

        Assert.True(worktrees[0].IsLocked);
        Assert.Equal("on a usb stick", worktrees[0].LockReason);
        Assert.True(worktrees[1].IsLocked);
        Assert.Null(worktrees[1].LockReason);
    }

    [Fact]
    public void GivenPrunableWorktree_WhenParsing_ThenFlaggedWithReason()
    {
        var output = Record("worktree /gone", "HEAD " + Hash, "branch refs/heads/gone", "prunable gitdir file points to non-existent location");

        var worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.True(worktree.IsPrunable);
        Assert.Equal("gitdir file points to non-existent location", worktree.PruneReason);
    }

    [Fact]
    public void GivenBareRepository_WhenParsing_ThenBareWithoutHead()
    {
        var worktree = Assert.Single(WorktreeParser.Parse(Record("worktree /srv/repo.git", "bare")).Items);

        Assert.True(worktree.IsBare);
        Assert.Null(worktree.HeadHash);
        Assert.True(worktree.IsMain);
    }

    [Fact]
    public void GivenUnknownAttributeAndMissingTerminator_WhenParsing_ThenWarnsAndKeepsRecord()
    {
        var output = "worktree /work/repo\0HEAD " + Hash + "\0future-thing 1";

        var result = WorktreeParser.Parse(output);

        Assert.Single(result.Items);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void GivenRecordWithoutPath_WhenParsing_ThenSkippedWithWarning()
    {
        var result = WorktreeParser.Parse(Record("HEAD " + Hash));

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }

    private static string Record(params string[] attributes) => string.Join('\0', attributes) + "\0\0";
}
