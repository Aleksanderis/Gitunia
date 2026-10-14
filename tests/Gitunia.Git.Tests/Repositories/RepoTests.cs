using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepoTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenWorkingTreeRoot_WhenOpening_ThenLocationIsResolved()
    {
        using var repository = TestRepositories.Linear(1);

        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var expected = Canonical(repository, repository.Path);
        Assert.Equal(expected, Normalize(repo.Location.WorkingTreePath!));
        Assert.Equal(Path.Combine(expected, ".git"), Normalize(repo.Location.GitDirectory));
        Assert.Equal(Normalize(repo.Location.GitDirectory), Normalize(repo.Location.CommonDirectory));
        Assert.False(repo.Location.IsBare);
        Assert.False(repo.Location.IsLinkedWorktree);
    }

    [Fact]
    public async Task GivenSubdirectory_WhenOpening_ThenResolvesToRepositoryRoot()
    {
        using var repository = TestRepository.Create().Commit("Nested", (Path.Combine("a", "b", "file.txt"), "x\n"));

        var repo = await Repo.OpenAsync(Runner, Path.Combine(repository.Path, "a", "b"), Token);

        Assert.Equal(Canonical(repository, repository.Path), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenBareRepository_WhenOpening_ThenThereIsNoWorkingTree()
    {
        using var repository = TestRepository.Create(bare: true);

        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.True(repo.Location.IsBare);
        Assert.Null(repo.Location.WorkingTreePath);
        Assert.Equal(Normalize(repo.Location.GitDirectory), Normalize(repo.Location.RootPath));
    }

    [Fact]
    public async Task GivenLinkedWorktree_WhenOpening_ThenItIsFlaggedAndSharesCommonDirectory()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);
        var main = await Repo.OpenAsync(Runner, repository.Path, Token);

        var linked = await Repo.OpenAsync(Runner, worktreePath, Token);

        Assert.True(linked.Location.IsLinkedWorktree);
        Assert.NotEqual(Normalize(main.Location.RootPath), Normalize(linked.Location.RootPath));
        Assert.Equal(Normalize(main.Location.CommonDirectory), Normalize(linked.Location.CommonDirectory));
        Assert.Equal(Canonical(repository, worktreePath), Normalize(linked.Location.WorkingTreePath!));
        Assert.Equal("wt-branch", (await linked.ReadHeadAsync(Token)).BranchName);
        Assert.Equal("main", (await main.ReadHeadAsync(Token)).BranchName);
    }

    [Fact]
    public async Task GivenPathInsideGitDirectory_WhenOpening_ThenWorkingTreeIsStillFound()
    {
        using var repository = TestRepositories.Linear(1);

        var repo = await Repo.OpenAsync(Runner, Path.Combine(repository.Path, ".git", "refs"), Token);

        Assert.Equal(Canonical(repository, repository.Path), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenPathInsideLinkedWorktreeGitDirectory_WhenOpening_ThenLinkedWorkingTreeIsFound()
    {
        using var repository = TestRepositories.WithWorktree(out var worktreePath);
        var linkedGitDirectory = Path.Combine(repository.Path, ".git", "worktrees", "wt");

        var repo = await Repo.OpenAsync(Runner, linkedGitDirectory, Token);

        Assert.True(repo.Location.IsLinkedWorktree);
        Assert.Equal(Canonical(repository, worktreePath), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenDirectoryOutsideAnyRepository_WhenOpening_ThenNotARepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<NotARepositoryException>(() => Repo.OpenAsync(Runner, directory, Token));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Fact]
    public async Task GivenMissingDirectory_WhenOpening_ThenNotARepository()
    {
        var missing = Path.Combine(Path.GetTempPath(), "gitunia-missing-" + Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<NotARepositoryException>(() => Repo.OpenAsync(Runner, missing, Token));
    }

    [Fact]
    public async Task GivenCommits_WhenReadingHead_ThenBranchWithHash()
    {
        using var repository = TestRepositories.Linear(2);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var head = await repo.ReadHeadAsync(Token);

        Assert.Equal(new HeadState(HeadKind.Branch, "main", repository.Head), head);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHead_ThenNoBranch()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Git("checkout", "-q", "--detach", "HEAD~1");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var head = await repo.ReadHeadAsync(Token);

        Assert.Equal(HeadKind.Detached, head.Kind);
        Assert.Null(head.BranchName);
        Assert.Equal(repository.Head, head.CommitHash);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingHead_ThenBranchWithoutCommit()
    {
        using var repository = TestRepository.Create();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var head = await repo.ReadHeadAsync(Token);

        Assert.Equal(new HeadState(HeadKind.Unborn, "main", null), head);
    }

    [Fact]
    public async Task GivenCleanRepository_WhenDetectingOperation_ThenNone()
    {
        using var repository = TestRepositories.Branchy();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.None, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenDetectingOperation_ThenMerge()
    {
        using var repository = TestRepositories.Conflicted();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.Merge, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedRebase_WhenDetectingOperation_ThenRebase()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git(["merge", "--abort"], null);
        repository.Git(["rebase", "other"], [1]);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.Rebase, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedCherryPick_WhenDetectingOperation_ThenCherryPick()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git(["merge", "--abort"], null);
        repository.Git(["cherry-pick", "other"], [1]);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.CherryPick, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedRevert_WhenDetectingOperation_ThenRevert()
    {
        using var repository = TestRepository.Create()
            .Commit("One", ("file.txt", "one\n"))
            .Commit("Two", ("file.txt", "two\n"))
            .Commit("Three", ("file.txt", "three\n"));
        repository.Git(["revert", "--no-edit", "HEAD~1"], [1]);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.Revert, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenBisectInProgress_WhenDetectingOperation_ThenBisect()
    {
        using var repository = TestRepositories.Linear(3);
        repository.Git("bisect", "start");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.Bisect, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenPathWithSpacesAndUnicode_WhenOpening_ThenResolves()
    {
        using var repository = TestRepository.Create().Commit("Odd", (Path.Combine("sub dir ünï", "file.txt"), "x\n"));

        var repo = await Repo.OpenAsync(Runner, Path.Combine(repository.Path, "sub dir ünï"), Token);

        Assert.Equal(Canonical(repository, repository.Path), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenLinkedWorktreeOfBareRepository_WhenOpening_ThenWorkingTreeIsLinkedAndCommonDirectoryIsBare()
    {
        using var source = TestRepositories.Linear(1);
        using var bare = TestRepository.Create(bare: true);
        source.Git("push", "-q", bare.Path, "main");
        var worktreePath = bare.AddWorktree("from-bare", "main");

        var repo = await Repo.OpenAsync(Runner, worktreePath, Token);
        var bareRepo = await Repo.OpenAsync(Runner, bare.Path, Token);

        Assert.True(repo.Location.IsLinkedWorktree);
        Assert.False(repo.Location.IsBare);
        Assert.Equal(Normalize(bareRepo.Location.GitDirectory), Normalize(repo.Location.CommonDirectory));
        Assert.Equal("main", (await repo.ReadHeadAsync(Token)).BranchName);
    }

    [Fact]
    public async Task GivenRelativeWorktreePointer_WhenOpeningInsideItsGitDirectory_ThenWorkingTreeIsFound()
    {
        using var repository = TestRepositories.Linear(1);
        var worktreePath = Path.Combine(Path.GetDirectoryName(repository.Path)!, "relative-wt");
        string output;
        try
        {
            output = repository.Git(["worktree", "add", "-q", "--relative-paths", "-b", "rel", worktreePath], null);
        }
        catch (GitCommandFailedException)
        {
            return; // git older than 2.48 has no --relative-paths
        }

        Assert.NotNull(output);
        var repo = await Repo.OpenAsync(Runner, Path.Combine(repository.Path, ".git", "worktrees", "relative-wt"), Token);

        Assert.Equal(Canonical(repository, worktreePath), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenSeparateGitDirectory_WhenOpeningInsideIt_ThenFailsUnlessCoreWorktreeIsSet()
    {
        using var repository = TestRepository.Create();
        var separate = Path.Combine(Path.GetDirectoryName(repository.Path)!, "separate.git");
        var worktree = Path.Combine(Path.GetDirectoryName(repository.Path)!, "separate-work");
        Directory.CreateDirectory(worktree);
        repository.Git("init", "-q", "-b", "main", "--separate-git-dir", separate, worktree);

        await Assert.ThrowsAsync<NotARepositoryException>(() => Repo.OpenAsync(Runner, separate, Token));

        repository.Git(["-C", separate, "config", "core.worktree", worktree], null);
        var repo = await Repo.OpenAsync(Runner, separate, Token);

        Assert.Equal(Canonical(repository, worktree), Normalize(repo.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenConflictInLinkedWorktree_WhenDetectingOperation_ThenOnlyThatWorktreeReportsIt()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", ("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other", ("conflict.txt", "other\n"))
            .Checkout("main");
        var worktreePath = repository.AddWorktree("work", "side", createBranch: true);
        repository.Git(["-C", worktreePath, "checkout", "-q", "-b", "side2"], null);
        File.WriteAllText(Path.Combine(worktreePath, "conflict.txt"), "side\n");
        repository.Git(["-C", worktreePath, "commit", "-q", "-am", "Side"], null);
        repository.Git(["-C", worktreePath, "merge", "-q", "other"], [1]);

        var linked = await Repo.OpenAsync(Runner, worktreePath, Token);
        var main = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.Merge, linked.DetectOperation());
        Assert.Equal(RepositoryOperation.None, main.DetectOperation());
    }

    [Fact]
    public async Task GivenMultiCommitCherryPickStopped_WhenDetectingOperation_ThenCherryPickFromSequencer()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", ("a.txt", "a\n"), ("b.txt", "b\n"))
            .CheckoutNewBranch("other")
            .Commit("Other a", ("a.txt", "other a\n"))
            .Commit("Other b", ("b.txt", "other b\n"))
            .Checkout("main")
            .Commit("Main a", ("a.txt", "main a\n"));
        repository.Git(["cherry-pick", "other~1", "other"], [1]);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(RepositoryOperation.CherryPick, repo.DetectOperation());
    }

    [Fact]
    public async Task GivenRebaseStopped_WhenReadingHead_ThenDetached()
    {
        using var repository = TestRepositories.Conflicted();
        repository.Git(["merge", "--abort"], null);
        repository.Git(["rebase", "other"], [1]);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Equal(HeadKind.Detached, (await repo.ReadHeadAsync(Token)).Kind);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private static string Canonical(TestRepository repository, string path) =>
        Normalize(repository.Git(["-C", path, "rev-parse", "--path-format=absolute", "--show-toplevel"], null).Trim());
}
