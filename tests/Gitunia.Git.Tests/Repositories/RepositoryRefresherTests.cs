using Gitunia.Git.Execution;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryRefresherTests
{
    private static readonly GitRunner Runner = IsolatedGit.CreateRunner();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly RepositoryRefresher Refresher = new(new RefReader(Runner), new StatusReader(Runner), new RepositoryOperationDetector());

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNoPreviousSnapshot_WhenRefreshing_ThenEverythingIsLoadedAndReportedChanged()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var result = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.None, Token);

        Assert.Equal(RepositorySections.All, result.Changed);
        Assert.Equal(testRepository.Head, result.Snapshot.Head.CommitHash);
        Assert.Contains(result.Snapshot.Refs, reference => reference.Name == "main");
    }

    [Fact]
    public async Task GivenNothingChanged_WhenRefreshingEverything_ThenNothingIsReportedChanged()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var first = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);

        var second = await Refresher.RefreshAsync(location, first.Snapshot, RepositoryChangeKinds.All, Token);

        Assert.Equal(RepositorySections.None, second.Changed);
        Assert.Same(first.Snapshot.Refs, second.Snapshot.Refs);
        Assert.Same(first.Snapshot.Status, second.Snapshot.Status);
    }

    [Fact]
    public async Task GivenUntrackedFile_WhenRefreshingWorkingTreeOnly_ThenOnlyStatusChanges()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var first = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);
        testRepository.WriteFile("new.txt", "x");

        var second = await Refresher.RefreshAsync(location, first.Snapshot, RepositoryChangeKinds.WorkingTree, Token);

        Assert.Equal(RepositorySections.Status, second.Changed);
        Assert.Same(first.Snapshot.Refs, second.Snapshot.Refs);
        Assert.Same(first.Snapshot.Worktrees, second.Snapshot.Worktrees);
        Assert.Contains(second.Snapshot.Status.Entries, entry => entry.Path == "new.txt");
    }

    [Fact]
    public async Task GivenNewCommit_WhenRefreshingRefs_ThenHeadRefsAndWorktreesChange()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var first = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);
        testRepository.Commit("next", new TestFile("n.txt", "n"));

        var second = await Refresher.RefreshAsync(location, first.Snapshot, RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs, Token);

        Assert.True(second.Changed.HasFlag(RepositorySections.Head));
        Assert.True(second.Changed.HasFlag(RepositorySections.Refs));
        Assert.True(second.Changed.HasFlag(RepositorySections.Worktrees));
        Assert.Equal(testRepository.Head, second.Snapshot.Head.CommitHash);
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenRefreshingOperation_ThenOperationChanges()
    {
        using var testRepository = TestRepositories.Conflicted();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var result = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);

        Assert.NotEqual(RepositoryOperation.None, result.Snapshot.Operation);
    }

    [Fact]
    public async Task GivenDetectorReportingAnOperation_WhenRefreshing_ThenItEndsUpInTheSnapshotAndTheChangedSections()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var detector = new StubOperationDetector();
        var refresher = new RepositoryRefresher(new RefReader(Runner), new StatusReader(Runner), detector);
        var first = await refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);
        detector.Operation = RepositoryOperation.Rebase;

        var second = await refresher.RefreshAsync(location, first.Snapshot, RepositoryChangeKinds.Operation, Token);

        Assert.Equal(RepositoryOperation.Rebase, second.Snapshot.Operation);
        Assert.Equal(RepositorySections.Operation, second.Changed);
    }

    [Fact]
    public async Task GivenAStatusOnlyRefresh_WhenRefreshing_ThenOneProcessRuns()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var first = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);
        var before = Runner.StartedCount;

        await Refresher.RefreshAsync(
            location,
            first.Snapshot,
            RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree,
            Token);

        Assert.Equal(1, Runner.StartedCount - before);
    }

    [Fact]
    public async Task GivenAFullRefresh_WhenRefreshing_ThenRefsWorktreesAndStatusAreTheOnlyProcesses()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var before = Runner.StartedCount;

        await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);

        Assert.Equal(3, Runner.StartedCount - before);
    }

    [Fact]
    public async Task GivenADetachedHead_WhenRefreshing_ThenHeadIsDetachedAtTheCommit()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Checkout("HEAD~1");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var result = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);

        Assert.Equal(HeadKind.Detached, result.Snapshot.Head.Kind);
        Assert.Null(result.Snapshot.Head.BranchName);
        Assert.Equal(testRepository.Git("rev-parse", "HEAD").Trim(), result.Snapshot.Head.CommitHash);
    }

    [Fact]
    public async Task GivenAnUnbornBranch_WhenRefreshing_ThenHeadIsUnbornWithoutACommit()
    {
        using var testRepository = TestRepository.Create();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var result = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);

        Assert.Equal(new HeadState(HeadKind.Unborn, "main", null), result.Snapshot.Head);
    }

    [Fact]
    public async Task GivenACheckedOutOtherBranch_WhenRefreshingHead_ThenTheBranchAndItsCommitChange()
    {
        using var testRepository = TestRepositories.Linear(2);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        var first = await Refresher.RefreshAsync(location, null, RepositoryChangeKinds.All, Token);
        testRepository.Branch("topic", "HEAD~1");
        testRepository.Checkout("topic");

        var second = await Refresher.RefreshAsync(location, first.Snapshot, RepositoryChangeKinds.Head, Token);

        Assert.True(second.Changed.HasFlag(RepositorySections.Head));
        Assert.Equal(new HeadState(HeadKind.Branch, "topic", testRepository.Git("rev-parse", "HEAD").Trim()), second.Snapshot.Head);
    }

    private sealed class StubOperationDetector : IRepositoryOperationDetector
    {
        public RepositoryOperation Operation { get; set; } = RepositoryOperation.None;

        public RepositoryOperation Detect(RepositoryLocation location) => Operation;
    }
}
