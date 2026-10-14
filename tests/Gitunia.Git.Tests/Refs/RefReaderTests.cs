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
        using var repository = TestRepositories.Branchy();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var result = await RefReader.ReadRefsAsync(repo, Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(["feature", "main", "topic", "v1"], result.Items.Select(reference => reference.Name).Order());
        var main = result.Items.Single(reference => reference.Name == "main");
        Assert.True(main.IsCurrent);
        Assert.Equal(repository.Head, main.CommitHash);
        Assert.NotNull(main.CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingRefs_ThenNoRefsAndOneWorktree()
    {
        using var repository = TestRepository.Create();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Empty((await RefReader.ReadRefsAsync(repo, Token)).Items);
        var worktree = Assert.Single((await RefReader.ReadWorktreesAsync(repo, Token)).Items);
        Assert.Equal("refs/heads/main", worktree.BranchFullName);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingRefs_ThenNoBranchIsCurrent()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Git("checkout", "-q", "--detach", "HEAD~1");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var result = await RefReader.ReadRefsAsync(repo, Token);

        Assert.DoesNotContain(result.Items, reference => reference.IsCurrent);
        Assert.Null(result.Items.Single(reference => reference.Name == "main").CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingWorktrees_ThenHeadHashIsNull()
    {
        using var repository = TestRepository.Create();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Null(Assert.Single((await RefReader.ReadWorktreesAsync(repo, Token)).Items).HeadHash);
    }

    [Fact]
    public async Task GivenLinkedWorktreeAndOddNames_WhenReadingRefs_ThenCheckedOutInAndNamesAreCorrect()
    {
        using var repository = TestRepositories.Linear(1);
        repository.Branch("feature/ünï_branch");
        repository.Tag("release/1.0");
        var worktreePath = repository.AddWorktree("wt with space", "feature/ünï_branch");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var refs = (await RefReader.ReadRefsAsync(repo, Token)).Items;

        var feature = refs.Single(reference => reference.Name == "feature/ünï_branch");
        Assert.Equal(Path.GetFullPath(worktreePath), Path.GetFullPath(feature.CheckedOutIn!));
        Assert.Equal(RefKind.Tag, refs.Single(reference => reference.Name == "release/1.0").Kind);
    }

    [Fact]
    public async Task GivenUpstreamAheadAndBehind_WhenReadingRefs_ThenCountsAreReported()
    {
        using var origin = TestRepository.Create(bare: true);
        using var local = TestRepository.Create().Commit("One", ("f.txt", "1\n"));
        local.Git("remote", "add", "origin", origin.Path);
        local.Git("push", "-q", "-u", "origin", "main");
        local.Commit("Two local", ("f.txt", "2\n"));
        // Make origin move independently: push a second commit built from a clone.
        var clonePath = Path.Combine(Path.GetDirectoryName(local.Path)!, "clone");
        local.Git("clone", "-q", origin.Path, clonePath);
        local.Git(["-C", clonePath, "-c", "user.name=T", "-c", "user.email=t@e.com", "commit", "-q", "--allow-empty", "-m", "Remote two"], null);
        local.Git("-C", clonePath, "push", "-q", "origin", "main");
        local.Git("fetch", "-q", "origin");
        var repo = await Repo.OpenAsync(Runner, local.Path, Token);

        var main = (await RefReader.ReadRefsAsync(repo, Token)).Items.Single(reference => reference.Name == "main");

        Assert.Equal("refs/remotes/origin/main", main.UpstreamFullName);
        Assert.Equal(1, main.Ahead);
        Assert.Equal(1, main.Behind);
    }

    [Fact]
    public async Task GivenLockedPrunableAndDetachedWorktrees_WhenReadingWorktrees_ThenEachStateIsReported()
    {
        using var repository = TestRepositories.Linear(2);
        var locked = repository.AddWorktree("locked-wt", "locked-branch", createBranch: true);
        var prunable = repository.AddWorktree("prunable-wt", "prunable-branch", createBranch: true);
        repository.AddWorktree("detached-wt", "HEAD~1", createBranch: false);
        repository.Git("worktree", "lock", "--reason", "keep me", locked);
        Directory.Delete(prunable, recursive: true);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var worktrees = (await RefReader.ReadWorktreesAsync(repo, Token)).Items;

        Assert.Equal(4, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.Equal("keep me", worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/locked-branch").LockReason);
        Assert.True(worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/prunable-branch").IsPrunable);
        Assert.Single(worktrees, worktree => worktree.IsDetached);
    }
}
