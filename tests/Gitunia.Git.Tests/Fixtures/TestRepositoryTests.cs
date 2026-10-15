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
    public void GivenAmbientGitIdentityAndConfig_WhenCommitting_ThenFixtureIdentityIsUsed()
    {
        var home = Path.Combine(Path.GetTempPath(), $"gitunia-home-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(home);
        var config = Path.Combine(home, "config");
        File.WriteAllText(config, "[user]\n\tname = Ambient\n");
        var previousName = Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME");
        var previousGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        try
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Ambient");
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", config);
            using var repository = TestRepositories.Linear(1);

            Assert.Equal("Test", repository.Git("log", "-1", "--format=%an|%cn").Trim().Split('|')[0]);
            Assert.Equal("Test", repository.Git("log", "-1", "--format=%an|%cn").Trim().Split('|')[1]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", previousName);
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", previousGlobal);
            Directory.Delete(home, recursive: true);
        }
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
    public void GivenOddPaths_WhenListingFiles_ThenAllArePresent()
    {
        using var repository = TestRepositories.OddPaths();

        var files = repository.Git("ls-files", "-z");

        Assert.Contains("ünï cødé.txt", files);
        Assert.Contains("quote's.txt", files);
        Assert.Contains("dir with space/nested file.txt", files);
        Assert.Contains("with space.txt", files);
    }
}
