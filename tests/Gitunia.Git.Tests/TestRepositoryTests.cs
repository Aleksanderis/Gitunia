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

        Assert.Equal(first.Head, second.Head);
        Assert.Equal(first.Git("log", "--all", "--format=%H"), second.Git("log", "--all", "--format=%H"));
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
    public async Task GivenWorktreeRepository_WhenRunnerListsWorktrees_ThenBothAreReported()
    {
        using TestRepository repository = TestRepositories.WithWorktree(out string worktreePath);
        var runner = new GitRunner();

        GitResult result = await runner.RunAsync(repository.Path, ["worktree", "list", "--porcelain"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains(Path.GetFileName(worktreePath), result.StandardOutputText);
        Assert.Contains("branch refs/heads/wt-branch", result.StandardOutputText);
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
        Assert.Contains("with space.txt", files);
    }
}
