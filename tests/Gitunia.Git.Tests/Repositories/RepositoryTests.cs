using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenWorkingTreeRoot_WhenOpening_ThenLocationIsResolved()
    {
        using var testRepository = TestRepositories.Linear(1);

        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var expected = Canonical(testRepository, testRepository.Path);
        Assert.Equal(expected, Normalize(repository.Location.WorkingTreePath!));
        Assert.Equal(Path.Combine(expected, ".git"), Normalize(repository.Location.GitDirectory));
        Assert.Equal(Normalize(repository.Location.GitDirectory), Normalize(repository.Location.CommonDirectory));
        Assert.False(repository.Location.IsBare);
        Assert.False(repository.Location.IsLinkedWorktree);
    }

    [Fact]
    public async Task GivenDefaultOptions_WhenCommandExitsWithOne_ThenItThrows()
    {
        using var testRepository = TestRepositories.Linear(1);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => repository.RunAsync(["config", "--get", "no.such.key"], null, Token));
    }

    [Fact]
    public async Task GivenAcceptedExitCode_WhenCommandExitsWithOne_ThenItReturnsTheResult()
    {
        using var testRepository = TestRepositories.Linear(1);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var result = await repository.RunAsync(["config", "--get", "no.such.key"], GitRunOptions.ReadOnlyRunAllowingNoMatch, Token);

        Assert.Equal(GitRunOptions.NoMatchExitCode, result.ExitCode);
    }

    [Fact]
    public async Task GivenSubdirectory_WhenOpening_ThenResolvesToRepositoryRoot()
    {
        using var testRepository = TestRepository.Create().Commit("Nested", new TestFile(Path.Combine("a", "b", "file.txt"), "x\n"));

        var repository = await Repository.OpenAsync(Runner, Path.Combine(testRepository.Path, "a", "b"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.Path), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenBareRepository_WhenOpening_ThenThereIsNoWorkingTree()
    {
        using var testRepository = TestRepository.Create(bare: true);

        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.True(repository.Location.IsBare);
        Assert.Null(repository.Location.WorkingTreePath);
        Assert.Equal(Normalize(repository.Location.GitDirectory), Normalize(repository.Location.RootPath));
    }

    [Fact]
    public async Task GivenLinkedWorktree_WhenOpening_ThenItIsFlaggedAndSharesCommonDirectory()
    {
        using var testRepository = TestRepositories.WithWorktree(out var worktreePath);
        var main = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var linked = await Repository.OpenAsync(Runner, worktreePath, Token);

        Assert.True(linked.Location.IsLinkedWorktree);
        Assert.NotEqual(Normalize(main.Location.RootPath), Normalize(linked.Location.RootPath));
        Assert.Equal(Normalize(main.Location.CommonDirectory), Normalize(linked.Location.CommonDirectory));
        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(linked.Location.WorkingTreePath!));
        Assert.Equal("wt-branch", (await linked.ReadHeadAsync(Token)).BranchName);
        Assert.Equal("main", (await main.ReadHeadAsync(Token)).BranchName);
    }

    [Fact]
    public async Task GivenPathInsideGitDirectory_WhenOpening_ThenWorkingTreeIsStillFound()
    {
        using var testRepository = TestRepositories.Linear(1);

        var repository = await Repository.OpenAsync(Runner, Path.Combine(testRepository.Path, ".git", "refs"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.Path), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenPathInsideLinkedWorktreeGitDirectory_WhenOpening_ThenLinkedWorkingTreeIsFound()
    {
        using var testRepository = TestRepositories.WithWorktree(out var worktreePath);
        var linkedGitDirectory = Path.Combine(testRepository.Path, ".git", "worktrees", "wt");

        var repository = await Repository.OpenAsync(Runner, linkedGitDirectory, Token);

        Assert.True(repository.Location.IsLinkedWorktree);
        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenDirectoryOutsideAnyRepository_WhenOpening_ThenNotARepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<NotARepositoryException>(() => Repository.OpenAsync(Runner, directory, Token));
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

        await Assert.ThrowsAsync<NotARepositoryException>(() => Repository.OpenAsync(Runner, missing, Token));
    }

    [Fact]
    public async Task GivenCommits_WhenReadingHead_ThenBranchWithHash()
    {
        using var testRepository = TestRepositories.Linear(2);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var head = await repository.ReadHeadAsync(Token);

        Assert.Equal(new HeadState(HeadKind.Branch, "main", testRepository.Head), head);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHead_ThenNoBranch()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var head = await repository.ReadHeadAsync(Token);

        Assert.Equal(HeadKind.Detached, head.Kind);
        Assert.Null(head.BranchName);
        Assert.Equal(testRepository.Head, head.CommitHash);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingHead_ThenBranchWithoutCommit()
    {
        using var testRepository = TestRepository.Create();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var head = await repository.ReadHeadAsync(Token);

        Assert.Equal(new HeadState(HeadKind.Unborn, "main", null), head);
    }

    [Fact]
    public async Task GivenCleanRepository_WhenDetectingOperation_ThenNone()
    {
        using var testRepository = TestRepositories.Branchy();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.None, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenDetectingOperation_ThenMerge()
    {
        using var testRepository = TestRepositories.Conflicted();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.Merge, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedRebase_WhenDetectingOperation_ThenRebase()
    {
        using var testRepository = TestRepositories.Conflicted();
        testRepository.Git(["merge", "--abort"], null);
        testRepository.Git(["rebase", "other"], [1]);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.Rebase, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedCherryPick_WhenDetectingOperation_ThenCherryPick()
    {
        using var testRepository = TestRepositories.Conflicted();
        testRepository.Git(["merge", "--abort"], null);
        testRepository.Git(["cherry-pick", "other"], [1]);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.CherryPick, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenConflictedRevert_WhenDetectingOperation_ThenRevert()
    {
        using var testRepository = TestRepository.Create()
            .Commit("One", new TestFile("file.txt", "one\n"))
            .Commit("Two", new TestFile("file.txt", "two\n"))
            .Commit("Three", new TestFile("file.txt", "three\n"));
        testRepository.Git(["revert", "--no-edit", "HEAD~1"], [1]);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.Revert, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenBisectInProgress_WhenDetectingOperation_ThenBisect()
    {
        using var testRepository = TestRepositories.Linear(3);
        testRepository.Git("bisect", "start");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.Bisect, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenPathWithSpacesAndUnicode_WhenOpening_ThenResolves()
    {
        using var testRepository = TestRepository.Create().Commit("Odd", new TestFile(Path.Combine("sub dir ünï", "file.txt"), "x\n"));

        var repository = await Repository.OpenAsync(Runner, Path.Combine(testRepository.Path, "sub dir ünï"), Token);

        Assert.Equal(Canonical(testRepository, testRepository.Path), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenLinkedWorktreeOfBareRepository_WhenOpening_ThenWorkingTreeIsLinkedAndCommonDirectoryIsBare()
    {
        using var source = TestRepositories.Linear(1);
        using var bare = TestRepository.Create(bare: true);
        source.Git("push", "-q", bare.Path, "main");
        var worktreePath = bare.AddWorktree("from-bare", "main");

        var repository = await Repository.OpenAsync(Runner, worktreePath, Token);
        var bareRepository = await Repository.OpenAsync(Runner, bare.Path, Token);

        Assert.True(repository.Location.IsLinkedWorktree);
        Assert.False(repository.Location.IsBare);
        Assert.Equal(Normalize(bareRepository.Location.GitDirectory), Normalize(repository.Location.CommonDirectory));
        Assert.Equal("main", (await repository.ReadHeadAsync(Token)).BranchName);
    }

    [Fact]
    public async Task GivenRelativeWorktreePointer_WhenOpeningInsideItsGitDirectory_ThenWorkingTreeIsFound()
    {
        using var testRepository = TestRepositories.Linear(1);
        var worktreePath = Path.Combine(Path.GetDirectoryName(testRepository.Path)!, "relative-wt");
        string output;
        try
        {
            output = testRepository.Git(["worktree", "add", "-q", "--relative-paths", "-b", "rel", worktreePath], null);
        }
        catch (GitCommandFailedException)
        {
            return; // git older than 2.48 has no --relative-paths
        }

        Assert.NotNull(output);
        var repository = await Repository.OpenAsync(Runner, Path.Combine(testRepository.Path, ".git", "worktrees", "relative-wt"), Token);

        Assert.Equal(Canonical(testRepository, worktreePath), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenSeparateGitDirectory_WhenOpeningInsideIt_ThenFailsUnlessCoreWorktreeIsSet()
    {
        using var testRepository = TestRepository.Create();
        var separate = Path.Combine(Path.GetDirectoryName(testRepository.Path)!, "separate.git");
        var worktree = Path.Combine(Path.GetDirectoryName(testRepository.Path)!, "separate-work");
        Directory.CreateDirectory(worktree);
        testRepository.Git("init", "-q", "-b", "main", "--separate-git-dir", separate, worktree);

        await Assert.ThrowsAsync<NotARepositoryException>(() => Repository.OpenAsync(Runner, separate, Token));

        testRepository.Git(["-C", separate, "config", "core.worktree", worktree], null);
        var repository = await Repository.OpenAsync(Runner, separate, Token);

        Assert.Equal(Canonical(testRepository, worktree), Normalize(repository.Location.WorkingTreePath!));
    }

    [Fact]
    public async Task GivenConflictInLinkedWorktree_WhenDetectingOperation_ThenOnlyThatWorktreeReportsIt()
    {
        using var testRepository = TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("Other", new TestFile("conflict.txt", "other\n"))
            .Checkout("main");
        var worktreePath = testRepository.AddWorktree("work", "side", createBranch: true);
        testRepository.Git(["-C", worktreePath, "checkout", "-q", "-b", "side2"], null);
        File.WriteAllText(Path.Combine(worktreePath, "conflict.txt"), "side\n");
        testRepository.Git(["-C", worktreePath, "commit", "-q", "-am", "Side"], null);
        testRepository.Git(["-C", worktreePath, "merge", "-q", "other"], [1]);

        var linked = await Repository.OpenAsync(Runner, worktreePath, Token);
        var main = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.Merge, linked.DetectOperation());
        Assert.Equal(RepositoryOperation.None, main.DetectOperation());
    }

    [Fact]
    public async Task GivenMultiCommitCherryPickStopped_WhenDetectingOperation_ThenCherryPickFromSequencer()
    {
        using var testRepository = TestRepository.Create()
            .Commit("Initial", new TestFile("a.txt", "a\n"), new TestFile("b.txt", "b\n"))
            .CheckoutNewBranch("other")
            .Commit("Other a", new TestFile("a.txt", "other a\n"))
            .Commit("Other b", new TestFile("b.txt", "other b\n"))
            .Checkout("main")
            .Commit("Main a", new TestFile("a.txt", "main a\n"));
        testRepository.Git(["cherry-pick", "other~1", "other"], [1]);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(RepositoryOperation.CherryPick, repository.DetectOperation());
    }

    [Fact]
    public async Task GivenRebaseStopped_WhenReadingHead_ThenDetached()
    {
        using var testRepository = TestRepositories.Conflicted();
        testRepository.Git(["merge", "--abort"], null);
        testRepository.Git(["rebase", "other"], [1]);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Equal(HeadKind.Detached, (await repository.ReadHeadAsync(Token)).Kind);
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private static string Canonical(TestRepository testRepository, string path) =>
        Normalize(testRepository.Git(["-C", path, "rev-parse", "--path-format=absolute", "--show-toplevel"], null).Trim());
}
