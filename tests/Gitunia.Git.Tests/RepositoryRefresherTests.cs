using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class RepositoryRefresherTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenNoPreviousSnapshot_WhenRefreshing_ThenEverythingIsLoadedAndReportedChanged()
    {
        using var repository = TestRepositories.Linear(2);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var result = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.None, Token);

        Assert.Equal(RepositorySections.All, result.Changed);
        Assert.Equal(repository.Head, result.Snapshot.Head.CommitHash);
        Assert.Contains(result.Snapshot.Refs, reference => reference.Name == "main");
    }

    [Fact]
    public async Task GivenNothingChanged_WhenRefreshingEverything_ThenNothingIsReportedChanged()
    {
        using var repository = TestRepositories.Linear(2);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var first = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.All, Token);

        var second = await RepositoryRefresher.RefreshAsync(repo, first.Snapshot, RepositoryChangeKinds.All, Token);

        Assert.Equal(RepositorySections.None, second.Changed);
        Assert.Same(first.Snapshot.Refs, second.Snapshot.Refs);
        Assert.Same(first.Snapshot.Status, second.Snapshot.Status);
    }

    [Fact]
    public async Task GivenUntrackedFile_WhenRefreshingWorkingTreeOnly_ThenOnlyStatusChanges()
    {
        using var repository = TestRepositories.Linear(2);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var first = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.All, Token);
        repository.WriteFile("new.txt", "x");

        var second = await RepositoryRefresher.RefreshAsync(repo, first.Snapshot, RepositoryChangeKinds.WorkingTree, Token);

        Assert.Equal(RepositorySections.Status, second.Changed);
        Assert.Same(first.Snapshot.Refs, second.Snapshot.Refs);
        Assert.Same(first.Snapshot.Worktrees, second.Snapshot.Worktrees);
        Assert.Contains(second.Snapshot.Status.Entries, entry => entry.Path == "new.txt");
    }

    [Fact]
    public async Task GivenNewCommit_WhenRefreshingRefs_ThenHeadRefsAndWorktreesChange()
    {
        using var repository = TestRepositories.Linear(2);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var first = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.All, Token);
        repository.Commit("next", ("n.txt", "n"));

        var second = await RepositoryRefresher.RefreshAsync(repo, first.Snapshot, RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs, Token);

        Assert.True(second.Changed.HasFlag(RepositorySections.Head));
        Assert.True(second.Changed.HasFlag(RepositorySections.Refs));
        Assert.True(second.Changed.HasFlag(RepositorySections.Worktrees));
        Assert.Equal(repository.Head, second.Snapshot.Head.CommitHash);
    }

    [Fact]
    public async Task GivenConflictedMerge_WhenRefreshingOperation_ThenOperationChanges()
    {
        using var repository = TestRepositories.Conflicted();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        var result = await RepositoryRefresher.RefreshAsync(repo, null, RepositoryChangeKinds.All, Token);

        Assert.NotEqual(RepositoryOperation.None, result.Snapshot.Operation);
    }
}
