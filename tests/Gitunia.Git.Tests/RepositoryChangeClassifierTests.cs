using Xunit;

namespace Gitunia.Git.Tests;

public class RepositoryChangeClassifierTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "classifier-root");

    private static readonly RepositoryLocation MainLocation = new(
        Path.Combine(Root, "repo"),
        Path.Combine(Root, "repo", ".git"),
        Path.Combine(Root, "repo", ".git"),
        false,
        false);

    private static readonly RepositoryLocation LinkedLocation = new(
        Path.Combine(Root, "linked"),
        Path.Combine(Root, "repo", ".git", "worktrees", "linked"),
        Path.Combine(Root, "repo", ".git"),
        false,
        true);

    [Theory]
    [InlineData(".git/HEAD", RepositoryChangeKinds.Head)]
    [InlineData(".git/index", RepositoryChangeKinds.Index)]
    [InlineData(".git/index.lock", RepositoryChangeKinds.None)]
    [InlineData(".git/refs/heads/main", RepositoryChangeKinds.Refs)]
    [InlineData(".git/packed-refs", RepositoryChangeKinds.Refs)]
    [InlineData(".git/MERGE_HEAD", RepositoryChangeKinds.Operation)]
    [InlineData(".git/rebase-merge/done", RepositoryChangeKinds.Operation)]
    [InlineData(".git/objects/ab/cdef", RepositoryChangeKinds.None)]
    [InlineData(".git/logs/HEAD", RepositoryChangeKinds.None)]
    [InlineData(".git/worktrees/other/HEAD", RepositoryChangeKinds.Worktrees)]
    [InlineData(".git/worktrees/other/index", RepositoryChangeKinds.None)]
    [InlineData("src/file.cs", RepositoryChangeKinds.WorkingTree)]
    public void GivenMainTab_WhenClassifying_ThenKindMatches(string relativePath, RepositoryChangeKinds expected)
    {
        var fullPath = Path.Combine(MainLocation.WorkingTreePath!, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.Equal(expected, RepositoryChangeClassifier.Classify(MainLocation, fullPath));
    }

    [Fact]
    public void GivenLinkedTab_WhenOwnGitDirectoryChanges_ThenItIsHeadAndIndexNotWorktrees()
    {
        var own = LinkedLocation.GitDirectory;

        Assert.Equal(RepositoryChangeKinds.Head, RepositoryChangeClassifier.Classify(LinkedLocation, Path.Combine(own, "HEAD")));
        Assert.Equal(RepositoryChangeKinds.Index, RepositoryChangeClassifier.Classify(LinkedLocation, Path.Combine(own, "index")));
    }

    [Fact]
    public void GivenLinkedTab_WhenMainRefsOrMainHeadChange_ThenRefsAndWorktrees()
    {
        var common = LinkedLocation.CommonDirectory;

        Assert.Equal(RepositoryChangeKinds.Refs, RepositoryChangeClassifier.Classify(LinkedLocation, Path.Combine(common, "refs", "heads", "x")));
        Assert.Equal(RepositoryChangeKinds.Worktrees, RepositoryChangeClassifier.Classify(LinkedLocation, Path.Combine(common, "HEAD")));
    }

    [Fact]
    public void GivenUnrelatedPath_WhenClassifying_ThenNone()
    {
        Assert.Equal(RepositoryChangeKinds.None, RepositoryChangeClassifier.Classify(MainLocation, Path.Combine(Root, "elsewhere", "file")));
        Assert.Equal(RepositoryChangeKinds.None, RepositoryChangeClassifier.Classify(MainLocation, Path.Combine(Root, "repository-sibling", "file")));
    }
}
