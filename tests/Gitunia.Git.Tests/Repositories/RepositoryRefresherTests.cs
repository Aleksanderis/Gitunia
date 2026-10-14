using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;
using Gitunia.Git.Refs;
using Gitunia.Git.Status;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryRefresherTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly RepositoryRefresher Refresher = new(new RefReader(Runner), new StatusReader(Runner));

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
}
