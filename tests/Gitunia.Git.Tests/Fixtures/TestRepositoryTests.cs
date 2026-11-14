using Xunit;

namespace Gitunia.Git.Tests.Fixtures;

public class TestRepositoryTests
{
    [Fact]
    public void GivenSameRecipe_WhenBuiltTwice_ThenCommitHashesAreIdentical()
    {
        using var first = TestRepositories.WithMergedFeatureBranch();
        using var second = TestRepositories.WithMergedFeatureBranch();

        Assert.Equal("ee569fa4c4631b3dcb1a69fc54cc5fa344956d4a", first.Head);

        Assert.Equal(first.Head, second.Head);
        Assert.Equal(first.Git("log", "--all", "--format=%H"), second.Git("log", "--all", "--format=%H"));
    }

    [Fact]
    public void GivenLinearRecipe_WhenBuilt_ThenHeadHashIsTheKnownConstant()
    {
        using var repository = TestRepositories.Linear();

        Assert.Equal("9581c98303acec73532d8e10d5eb6ee6dff69d50", repository.Head);
    }

    [Fact]
    public void GivenMergedFeatureBranchRepository_WhenListingRefs_ThenShapeMatches()
    {
        using var repository = TestRepositories.WithMergedFeatureBranch();

        var branches = repository.Git("for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/tags");

        Assert.Equal(["feature", "main", "topic", "v1"], branches.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Single(repository.Git("rev-list", "--min-parents=2", "HEAD").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void GivenWorktreeRepository_WhenListingWorktrees_ThenBothAreReported()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);
        var output = repository.Git("worktree", "list", "--porcelain");

        Assert.Contains(Path.GetFileName(worktreePath), output);
        Assert.Contains("branch refs/heads/wt-branch", output);
    }

    [Fact]
    public void GivenConflictedRepository_WhenReadingStatus_ThenFileIsUnmerged()
    {
        using var repository = TestRepositories.Conflicted();

        var status = repository.Git("status", "--porcelain");

        Assert.StartsWith("UU conflict.txt", status);
    }

    [Fact]
    public void GivenOriginRepository_WhenReadingTracking_ThenMainTracksBareOriginInSync()
    {
        using var repository = TestRepositories.WithOrigin();

        Assert.Equal("origin/main", repository.Git("rev-parse", "--abbrev-ref", "main@{upstream}").Trim());
        Assert.Equal("0\t0", repository.Git("rev-list", "--left-right", "--count", "main...origin/main").Trim());
        Assert.Equal("true", repository.GitIn(repository.OriginPath, ["rev-parse", "--is-bare-repository"]).Trim());
    }

    [Theory]
    [InlineData(2, 3)]
    [InlineData(1, 0)]
    [InlineData(0, 2)]
    public void GivenTrackingBranch_WhenCounting_ThenAheadAndBehindMatch(int ahead, int behind)
    {
        using var repository = TestRepositories.WithTrackingBranch(ahead: ahead, behind: behind);

        Assert.Equal(
            $"{ahead}\t{behind}",
            repository.Git("rev-list", "--left-right", "--count", "topic...origin/topic").Trim());
    }

    [Fact]
    public void GivenBranchWithoutUpstream_WhenReadingUpstream_ThenNoneIsConfigured()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();

        Assert.Empty(repository.Git("for-each-ref", "--format=%(upstream)", "refs/heads/lonely").Trim());
        Assert.Equal("origin/main", repository.Git("rev-parse", "--abbrev-ref", "main@{upstream}").Trim());
    }

    [Fact]
    public void GivenManyWorktrees_WhenListing_ThenLockedPrunableAndDirtyAreVisible()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);

        var listing = repository.Git("worktree", "list", "--porcelain");
        var blocks = listing.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, blocks.Length);
        Assert.Contains(blocks, block => block.Contains("wt-locked") && block.Contains("locked test lock"));
        Assert.Contains(
            blocks,
            block => block.Contains("wt-prunable") && block.Split('\n').Any(line => line.StartsWith("prunable")));
        Assert.Contains(" M file.txt", repository.GitIn(dirtyPath, ["status", "--porcelain"]));
    }

    [Fact]
    public void GivenRebaseConflicted_WhenInspecting_ThenRebaseIsStoppedWithUnmergedFile()
    {
        using var repository = TestRepositories.RebaseConflicted();

        var rebaseDirectory = Path.Combine(repository.WorkingTreePath, ".git", "rebase-merge");

        Assert.True(Directory.Exists(rebaseDirectory));
        Assert.Equal("refs/heads/other", File.ReadAllText(Path.Combine(rebaseDirectory, "head-name")).Trim());
        Assert.StartsWith("UU conflict.txt", repository.Git("status", "--porcelain"));
    }

    [Fact]
    public void GivenCherryPickConflicted_WhenInspecting_ThenPickIsInProgress()
    {
        using var repository = TestRepositories.CherryPickConflicted();

        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, ".git", "CHERRY_PICK_HEAD")));
        Assert.StartsWith("UU conflict.txt", repository.Git("status", "--porcelain"));
    }

    [Fact]
    public void GivenRevertConflicted_WhenInspecting_ThenRevertIsInProgress()
    {
        using var repository = TestRepositories.RevertConflicted();

        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, ".git", "REVERT_HEAD")));
        Assert.StartsWith("UU conflict.txt", repository.Git("status", "--porcelain"));
    }

    [Fact]
    public void GivenStashFixture_WhenListingStashAndStatus_ThenOneStashAndCleanTree()
    {
        using var repository = TestRepositories.WithStash();

        Assert.Single(repository.Git("stash", "list").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Empty(repository.Git("status", "--porcelain"));
        Assert.Contains("untracked.txt", repository.Git("show", "--name-only", "--format=", "stash@{0}^3"));
        Assert.Contains("file.txt", repository.Git("show", "--name-only", "--format=", "stash@{0}"));
    }

    [Fact]
    public void GivenOddPaths_WhenListingFiles_ThenAllArePresent()
    {
        using var repository = TestRepositories.OddPaths();

        var files = repository.Git("ls-files", "-z");

        Assert.Contains("ünï cødé.txt", files);
        Assert.Contains("quote's.txt", files);
        Assert.Contains("dir with space/nested file.txt", files);
        Assert.Contains("with space.txt", files);
    }

    [Fact]
    public void GivenTwoCopiesOfATemplate_WhenCommittingInOne_ThenTheOtherIsUnchangedAndTheClockContinues()
    {
        using var first = TestRepositories.Linear(2);
        using var second = TestRepositories.Linear(2);
        var original = second.Head;

        first.Commit("Extra", new TestFile("extra.txt", "x\n"));
        using var third = TestRepositories.Linear(2).Commit("Extra", new TestFile("extra.txt", "x\n"));

        Assert.Equal(original, second.Head);
        Assert.NotEqual(original, first.Head);
        Assert.Equal(first.Head, third.Head);
        Assert.NotEqual(first.WorkingTreePath, second.WorkingTreePath);
    }

    [Fact]
    public void GivenOriginTemplate_WhenCopied_ThenTheRemoteIsTheCopiesOwnBareRepository()
    {
        using var first = TestRepositories.WithOrigin();
        using var second = TestRepositories.WithOrigin();

        var remote = second.Git("remote", "get-url", "origin").Trim();
        second.Commit("Only in the copy", new TestFile("copy.txt", "c\n"));
        second.Git("push", "-q", "origin", "main");

        Assert.Equal(second.OriginPath, remote);
        Assert.Equal(first.Git("rev-parse", "main").Trim(), first.GitIn(first.OriginPath, ["rev-parse", "main"]).Trim());
        Assert.Equal(second.Head, second.GitIn(second.OriginPath, ["rev-parse", "main"]).Trim());
    }

    [Fact]
    public void GivenWorktreeTemplate_WhenCopied_ThenTheLinkedWorktreeBelongsToTheCopy()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);

        var listing = repository.Git("worktree", "list", "--porcelain");

        Assert.Contains("wt-branch", repository.GitIn(worktreePath, ["rev-parse", "--abbrev-ref", "HEAD"]));
        Assert.Equal(
            Path.GetFullPath(repository.GitIn(worktreePath, ["rev-parse", "--git-common-dir"]).Trim()),
            Path.GetFullPath(Path.Combine(repository.WorkingTreePath, ".git")));
        Assert.Contains(Path.GetFileName(worktreePath), listing);
        Assert.DoesNotContain("prunable", listing);
    }
}
