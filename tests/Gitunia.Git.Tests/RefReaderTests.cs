using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class RefReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenBranchyRepository_WhenReadingRefs_ThenBranchesAndTagAreListed()
    {
        using TestRepository repository = TestRepositories.Branchy();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        ParseResult<GitRef> result = await RefReader.ReadRefsAsync(repo, Token);

        Assert.Empty(result.Warnings);
        Assert.Equal(["feature", "main", "topic", "v1"], result.Items.Select(reference => reference.Name).Order());
        GitRef main = result.Items.Single(reference => reference.Name == "main");
        Assert.True(main.IsCurrent);
        Assert.Equal(repository.Head, main.CommitHash);
        Assert.NotNull(main.CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingRefs_ThenNoRefsAndOneWorktree()
    {
        using TestRepository repository = TestRepository.Create();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Empty((await RefReader.ReadRefsAsync(repo, Token)).Items);
        WorktreeInfo worktree = Assert.Single((await RefReader.ReadWorktreesAsync(repo, Token)).Items);
        Assert.Equal("refs/heads/main", worktree.BranchFullName);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingRefs_ThenNoBranchIsCurrent()
    {
        using TestRepository repository = TestRepositories.Linear(2);
        repository.Git("checkout", "-q", "--detach", "HEAD~1");
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        ParseResult<GitRef> result = await RefReader.ReadRefsAsync(repo, Token);

        Assert.DoesNotContain(result.Items, reference => reference.IsCurrent);
        Assert.Null(result.Items.Single(reference => reference.Name == "main").CheckedOutIn);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingWorktrees_ThenHeadHashIsNull()
    {
        using TestRepository repository = TestRepository.Create();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Null(Assert.Single((await RefReader.ReadWorktreesAsync(repo, Token)).Items).HeadHash);
    }

    [Fact]
    public async Task GivenLinkedWorktreeAndOddNames_WhenReadingRefs_ThenCheckedOutInAndNamesAreCorrect()
    {
        using TestRepository repository = TestRepositories.Linear(1);
        repository.Branch("feature/ünï_branch");
        repository.Tag("release/1.0");
        string worktreePath = repository.AddWorktree("wt with space", "feature/ünï_branch");
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        IReadOnlyList<GitRef> refs = (await RefReader.ReadRefsAsync(repo, Token)).Items;

        GitRef feature = refs.Single(reference => reference.Name == "feature/ünï_branch");
        Assert.Equal(Path.GetFullPath(worktreePath), Path.GetFullPath(feature.CheckedOutIn!));
        Assert.Equal(RefKind.Tag, refs.Single(reference => reference.Name == "release/1.0").Kind);
    }

    [Fact]
    public async Task GivenUpstreamAheadAndBehind_WhenReadingRefs_ThenCountsAreReported()
    {
        using TestRepository origin = TestRepository.Create(bare: true);
        using TestRepository local = TestRepository.Create().Commit("One", ("f.txt", "1\n"));
        local.Git("remote", "add", "origin", origin.Path);
        local.Git("push", "-q", "-u", "origin", "main");
        local.Commit("Two local", ("f.txt", "2\n"));
        // Make origin move independently: push a second commit built from a clone.
        string clonePath = Path.Combine(Path.GetDirectoryName(local.Path)!, "clone");
        local.Git("clone", "-q", origin.Path, clonePath);
        local.Git(["-C", clonePath, "-c", "user.name=T", "-c", "user.email=t@e.com", "commit", "-q", "--allow-empty", "-m", "Remote two"], null);
        local.Git("-C", clonePath, "push", "-q", "origin", "main");
        local.Git("fetch", "-q", "origin");
        Repo repo = await Repo.OpenAsync(Runner, local.Path, Token);

        GitRef main = (await RefReader.ReadRefsAsync(repo, Token)).Items.Single(reference => reference.Name == "main");

        Assert.Equal("refs/remotes/origin/main", main.UpstreamFullName);
        Assert.Equal(1, main.Ahead);
        Assert.Equal(1, main.Behind);
    }

    [Fact]
    public async Task GivenLockedPrunableAndDetachedWorktrees_WhenReadingWorktrees_ThenEachStateIsReported()
    {
        using TestRepository repository = TestRepositories.Linear(2);
        string locked = repository.AddWorktree("locked-wt", "locked-branch", createBranch: true);
        string prunable = repository.AddWorktree("prunable-wt", "prunable-branch", createBranch: true);
        repository.AddWorktree("detached-wt", "HEAD~1", createBranch: false);
        repository.Git("worktree", "lock", "--reason", "keep me", locked);
        Directory.Delete(prunable, recursive: true);
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        IReadOnlyList<WorktreeInfo> worktrees = (await RefReader.ReadWorktreesAsync(repo, Token)).Items;

        Assert.Equal(4, worktrees.Count);
        Assert.True(worktrees[0].IsMain);
        Assert.Equal("keep me", worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/locked-branch").LockReason);
        Assert.True(worktrees.Single(worktree => worktree.BranchFullName == "refs/heads/prunable-branch").IsPrunable);
        Assert.Single(worktrees, worktree => worktree.IsDetached);
    }
}
