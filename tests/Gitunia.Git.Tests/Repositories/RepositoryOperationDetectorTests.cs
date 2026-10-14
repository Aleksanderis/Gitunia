using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryOperationDetectorTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly RepositoryOperationDetector Detector = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenCleanRepository_WhenDetectingOperation_ThenNone()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.None, Detector.Detect(location));
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenDetectingOperation_ThenMerge()
    {
        using var testRepository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.Merge, Detector.Detect(location));
    }

    [Fact]
    public async Task GivenConflictedRebase_WhenDetectingOperation_ThenRebase()
    {
        using var testRepository = TestRepositories.Conflicted();
        testRepository.Git(["merge", "--abort"], null);
        testRepository.Git(["rebase", "other"], [1]);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.Rebase, Detector.Detect(location));
    }

    [Fact]
    public async Task GivenConflictedCherryPick_WhenDetectingOperation_ThenCherryPick()
    {
        using var testRepository = TestRepositories.Conflicted();
        testRepository.Git(["merge", "--abort"], null);
        testRepository.Git(["cherry-pick", "other"], [1]);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.CherryPick, Detector.Detect(location));
    }

    [Fact]
    public async Task GivenConflictedRevert_WhenDetectingOperation_ThenRevert()
    {
        using var testRepository = TestRepository.Create()
            .Commit("One", new TestFile("file.txt", "one\n"))
            .Commit("Two", new TestFile("file.txt", "two\n"))
            .Commit("Three", new TestFile("file.txt", "three\n"));
        testRepository.Git(["revert", "--no-edit", "HEAD~1"], [1]);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.Revert, Detector.Detect(location));
    }

    [Fact]
    public async Task GivenBisectInProgress_WhenDetectingOperation_ThenBisect()
    {
        using var testRepository = TestRepositories.Linear(3);
        testRepository.Git("bisect", "start");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.Bisect, Detector.Detect(location));
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

        var linked = await Discovery.DiscoverAsync(worktreePath, Token);
        var main = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.Merge, Detector.Detect(linked));
        Assert.Equal(RepositoryOperation.None, Detector.Detect(main));
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
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Equal(RepositoryOperation.CherryPick, Detector.Detect(location));
    }
}
