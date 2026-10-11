using Xunit;

namespace Gitunia.Git.Tests;

public class WorktreeParserTests
{
    private const string Hash = "1111111111111111111111111111111111111111";

    private static string Record(params string[] attributes) => string.Join('\0', attributes) + "\0\0";

    [Fact]
    public void GivenMainAndLinkedWorktrees_WhenParsing_ThenFirstIsMain()
    {
        string output = Record("worktree /work/repo", "HEAD " + Hash, "branch refs/heads/main")
            + Record("worktree /work/wt with space", "HEAD " + Hash, "branch refs/heads/feature");

        IReadOnlyList<WorktreeInfo> worktrees = WorktreeParser.Parse(output).Items;

        Assert.Equal(2, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.False(worktrees[1].IsMain);
        Assert.Equal("/work/wt with space", worktrees[1].Path);
        Assert.Equal("refs/heads/feature", worktrees[1].BranchFullName);
    }

    [Fact]
    public void GivenUnbornBranch_WhenParsing_ThenHeadHashIsNull()
    {
        string output = Record("worktree /work/repo", "HEAD 0000000000000000000000000000000000000000", "branch refs/heads/main");

        WorktreeInfo worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.Null(worktree.HeadHash);
        Assert.Equal("refs/heads/main", worktree.BranchFullName);
    }

    [Fact]
    public void GivenDetachedHead_WhenParsing_ThenNoBranch()
    {
        string output = Record("worktree /work/repo", "HEAD " + Hash, "detached");

        WorktreeInfo worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.True(worktree.IsDetached);
        Assert.Null(worktree.BranchFullName);
    }

    [Fact]
    public void GivenLockedWithAndWithoutReason_WhenParsing_ThenReasonIsKept()
    {
        string output = Record("worktree /a", "HEAD " + Hash, "branch refs/heads/a", "locked on a usb stick")
            + Record("worktree /b", "HEAD " + Hash, "branch refs/heads/b", "locked");

        IReadOnlyList<WorktreeInfo> worktrees = WorktreeParser.Parse(output).Items;

        Assert.True(worktrees[0].IsLocked);
        Assert.Equal("on a usb stick", worktrees[0].LockReason);
        Assert.True(worktrees[1].IsLocked);
        Assert.Null(worktrees[1].LockReason);
    }

    [Fact]
    public void GivenPrunableWorktree_WhenParsing_ThenFlaggedWithReason()
    {
        string output = Record("worktree /gone", "HEAD " + Hash, "branch refs/heads/gone", "prunable gitdir file points to non-existent location");

        WorktreeInfo worktree = Assert.Single(WorktreeParser.Parse(output).Items);

        Assert.True(worktree.IsPrunable);
        Assert.Equal("gitdir file points to non-existent location", worktree.PruneReason);
    }

    [Fact]
    public void GivenBareRepository_WhenParsing_ThenBareWithoutHead()
    {
        WorktreeInfo worktree = Assert.Single(WorktreeParser.Parse(Record("worktree /srv/repo.git", "bare")).Items);

        Assert.True(worktree.IsBare);
        Assert.Null(worktree.HeadHash);
        Assert.True(worktree.IsMain);
    }

    [Fact]
    public void GivenUnknownAttributeAndMissingTerminator_WhenParsing_ThenWarnsAndKeepsRecord()
    {
        string output = "worktree /work/repo\0HEAD " + Hash + "\0future-thing 1";

        ParseResult<WorktreeInfo> result = WorktreeParser.Parse(output);

        Assert.Single(result.Items);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public void GivenRecordWithoutPath_WhenParsing_ThenSkippedWithWarning()
    {
        ParseResult<WorktreeInfo> result = WorktreeParser.Parse(Record("HEAD " + Hash));

        Assert.Empty(result.Items);
        Assert.Single(result.Warnings);
    }
}
