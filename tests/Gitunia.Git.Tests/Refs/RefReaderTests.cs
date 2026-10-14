using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Refs;

public class RefReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenBranchyRepository_WhenReadingRefs_ThenBranchesAndTagAreListed()
    {
        using var testRepository = TestRepositories.Branchy();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var result = await RefReader.ReadRefsAsync(repository, Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(["feature", "main", "topic", "v1"], result.Items.Select(reference => reference.Name).Order());
        var main = result.Items.Single(reference => reference.Name == "main");
        Assert.True(main.IsCurrent);
        Assert.Equal(testRepository.Head, main.CommitHash);
        Assert.NotNull(main.CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingRefs_ThenNoRefsAndOneWorktree()
    {
        using var testRepository = TestRepository.Create();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Empty((await RefReader.ReadRefsAsync(repository, Token)).Items);
        var worktree = Assert.Single((await RefReader.ReadWorktreesAsync(repository, Token)).Items);
        Assert.Equal("refs/heads/main", worktree.BranchFullName);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingRefs_ThenNoBranchIsCurrent()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var result = await RefReader.ReadRefsAsync(repository, Token);

        Assert.DoesNotContain(result.Items, reference => reference.IsCurrent);
        Assert.Null(result.Items.Single(reference => reference.Name == "main").CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingWorktrees_ThenHeadHashIsNull()
    {
        using var testRepository = TestRepository.Create();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Null(Assert.Single((await RefReader.ReadWorktreesAsync(repository, Token)).Items).HeadHash);
    }

    [Fact]
    public async Task GivenLinkedWorktreeAndOddNames_WhenReadingRefs_ThenCheckedOutInAndNamesAreCorrect()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Branch("feature/ünï_branch");
        testRepository.Tag("release/1.0");
        var worktreePath = testRepository.AddWorktree("wt with space", "feature/ünï_branch");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var refs = (await RefReader.ReadRefsAsync(repository, Token)).Items;

        var feature = refs.Single(reference => reference.Name == "feature/ünï_branch");
        Assert.Equal(Path.GetFullPath(worktreePath), Path.GetFullPath(feature.CheckedOutIn!));
        Assert.Equal(RefKind.Tag, refs.Single(reference => reference.Name == "release/1.0").Kind);
    }

    [Fact]
    public async Task GivenUpstreamAheadAndBehind_WhenReadingRefs_ThenCountsAreReported()
    {
        using var origin = TestRepository.Create(bare: true);
        using var local = TestRepository.Create().Commit("One", new TestFile("f.txt", "1\n"));
        local.Git("remote", "add", "origin", origin.Path);
        local.Git("push", "-q", "-u", "origin", "main");
        local.Commit("Two local", new TestFile("f.txt", "2\n"));
        // Make origin move independently: push a second commit built from a clone.
        var clonePath = Path.Combine(Path.GetDirectoryName(local.Path)!, "clone");
        local.Git("clone", "-q", origin.Path, clonePath);
        local.Git(["-C", clonePath, "-c", "user.name=T", "-c", "user.email=t@e.com", "commit", "-q", "--allow-empty", "-m", "Remote two"], null);
        local.Git("-C", clonePath, "push", "-q", "origin", "main");
        local.Git("fetch", "-q", "origin");
        var repository = await Repository.OpenAsync(Runner, local.Path, Token);

        var main = (await RefReader.ReadRefsAsync(repository, Token)).Items.Single(reference => reference.Name == "main");

        Assert.Equal("refs/remotes/origin/main", main.UpstreamFullName);
        Assert.Equal(1, main.Ahead);
        Assert.Equal(1, main.Behind);
    }

    [Fact]
    public async Task GivenLockedPrunableAndDetachedWorktrees_WhenReadingWorktrees_ThenEachStateIsReported()
    {
        using var testRepository = TestRepositories.Linear(2);
        var locked = testRepository.AddWorktree("locked-wt", "locked-branch", createBranch: true);
        var prunable = testRepository.AddWorktree("prunable-wt", "prunable-branch", createBranch: true);
        testRepository.AddWorktree("detached-wt", "HEAD~1", createBranch: false);
        testRepository.Git("worktree", "lock", "--reason", "keep me", locked);
        Directory.Delete(prunable, recursive: true);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var worktrees = (await RefReader.ReadWorktreesAsync(repository, Token)).Items;

        Assert.Equal(4, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.Equal("keep me", worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/locked-branch").LockReason);
        Assert.True(worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/prunable-branch").IsPrunable);
        Assert.Single(worktrees, worktree => worktree.IsDetached);
    }
}
