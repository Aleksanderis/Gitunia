using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class TestRepositoryTests
{
    [Fact]
    public void GivenSameRecipe_WhenBuiltTwice_ThenCommitHashesAreIdentical()
    {
        using TestRepository first = TestRepositories.Branchy();
        using TestRepository second = TestRepositories.Branchy();

        Assert.Equal("ee569fa4c4631b3dcb1a69fc54cc5fa344956d4a", first.Head);

        Assert.Equal(first.Head, second.Head);
        Assert.Equal(first.Git("log", "--all", "--format=%H"), second.Git("log", "--all", "--format=%H"));
    }

    [Fact]
    public void GivenLinearRecipe_WhenBuilt_ThenHeadHashIsTheKnownConstant()
    {
        using TestRepository repository = TestRepositories.Linear(3);

        Assert.Equal("9581c98303acec73532d8e10d5eb6ee6dff69d50", repository.Head);
    }

    [Fact]
    public void GivenAmbientGitIdentityAndConfig_WhenCommitting_ThenFixtureIdentityIsUsed()
    {
        string home = Path.Combine(Path.GetTempPath(), "gitunia-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        string config = Path.Combine(home, "config");
        File.WriteAllText(config, "[user]\n\tname = Ambient\n");
        string? previousName = Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME");
        string? previousGlobal = Environment.GetEnvironmentVariable("GIT_CONFIG_GLOBAL");
        try
        {
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Ambient");
            Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", config);
            using TestRepository repository = TestRepositories.Linear(1);

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
    public void GivenBranchyRepository_WhenListingRefs_ThenShapeMatches()
    {
        using TestRepository repository = TestRepositories.Branchy();

        string branches = repository.Git("for-each-ref", "--format=%(refname:short)", "refs/heads", "refs/tags");

        Assert.Equal(["feature", "main", "topic", "v1"], branches.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Single(repository.Git("rev-list", "--min-parents=2", "HEAD").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void GivenWorktreeRepository_WhenListingWorktrees_ThenBothAreReported()
    {
        using TestRepository repository = TestRepositories.WithWorktree(out string worktreePath);
        string output = repository.Git("worktree", "list", "--porcelain");

        Assert.Contains(Path.GetFileName(worktreePath), output);
        Assert.Contains("branch refs/heads/wt-branch", output);
    }

    [Fact]
    public void GivenConflictedRepository_WhenReadingStatus_ThenFileIsUnmerged()
    {
        using TestRepository repository = TestRepositories.Conflicted();

        string status = repository.Git("status", "--porcelain");

        Assert.StartsWith("UU conflict.txt", status);
    }

    [Fact]
    public void GivenOddPaths_WhenListingFiles_ThenAllArePresent()
    {
        using TestRepository repository = TestRepositories.OddPaths();

        string files = repository.Git("ls-files", "-z");

        Assert.Contains("ünï cødé.txt", files);
        Assert.Contains("quote's.txt", files);
        Assert.Contains("dir with space/nested file.txt", files);
        Assert.Contains("with space.txt", files);
    }
}
