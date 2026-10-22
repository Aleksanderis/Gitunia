using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Worktrees;
using Gitunia.Git.Worktrees.Models;
using Xunit;

namespace Gitunia.Git.Tests.Worktrees;

public class WorktreeWriterTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly WorktreeWriter Writer = new(Runner);
    private static readonly RefReader Refs = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenStartPoint_WhenAddingNewBranch_ThenWorktreeHasBranchAtThatCommit()
    {
        using var repository = TestRepositories.Linear();
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var path = NewPath(repository, "added");
        var start = repository.Git("rev-parse", "HEAD~1").Trim();

        await Writer.AddAsync(location, new WorktreeAddRequest(path, "topic", start), Token);

        Assert.Equal("topic", repository.GitIn(path, ["branch", "--show-current"]).Trim());
        Assert.Equal(start, repository.GitIn(path, ["rev-parse", "HEAD"]).Trim());
        var worktrees = await Refs.ReadWorktreesAsync(location, Token);
        Assert.Contains(worktrees.Items, worktree => worktree.BranchFullName == "refs/heads/topic");
    }

    [Fact]
    public async Task GivenExistingBranch_WhenAdding_ThenWorktreeChecksItOut()
    {
        using var repository = TestRepositories.Linear(2).Branch("existing");
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var path = NewPath(repository, "existing-wt");

        await Writer.AddAsync(location, new WorktreeAddRequest(path, "existing", null), Token);

        Assert.Equal("existing", repository.GitIn(path, ["branch", "--show-current"]).Trim());
    }

    [Fact]
    public async Task GivenBranchHeldByAnotherWorktree_WhenAdding_ThenGitRefusesAndNothingIsCreated()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out _);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);
        var path = NewPath(repository, "second-checkout");

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Writer.AddAsync(location, new WorktreeAddRequest(path, "locked", null), Token));

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenCleanWorktree_WhenRemoving_ThenDirectoryGoneAndBranchKept()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("clean", "clean-branch", createBranch: true);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Writer.RemoveAsync(location, path, force: false, Token);

        Assert.False(Directory.Exists(path));
        Assert.Contains("clean-branch", repository.Git("branch", "--list", "clean-branch"));
    }

    [Fact]
    public async Task GivenDirtyWorktree_WhenRemovingWithoutForce_ThenGitRefusesAndFilesStay()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Writer.RemoveAsync(location, dirtyPath, force: false, Token));

        Assert.True(File.Exists(Path.Combine(dirtyPath, "file.txt")));
    }

    [Fact]
    public async Task GivenDirtyWorktree_WhenRemovingWithForce_ThenDirectoryIsGone()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Writer.RemoveAsync(location, dirtyPath, force: true, Token);

        Assert.False(Directory.Exists(dirtyPath));
    }

    [Fact]
    public async Task GivenLockedWorktree_WhenRemovingEvenWithForce_ThenItStays()
    {
        using var repository = TestRepositories.WithManyWorktrees(out var lockedPath, out _, out _);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => Writer.RemoveAsync(location, lockedPath, force: true, Token));

        Assert.True(Directory.Exists(lockedPath));
    }

    [Fact]
    public async Task GivenWorktreeWithChanges_WhenListingChanges_ThenModifiedAndUntrackedPathsAreListed()
    {
        using var repository = TestRepositories.WithManyWorktrees(out _, out _, out var dirtyPath);
        File.WriteAllText(Path.Combine(dirtyPath, "new file.txt"), "untracked\n");

        var changes = await Writer.ListChangesAsync(dirtyPath, Token);

        Assert.Equal(["file.txt", "new file.txt"], changes.Order());
    }

    [Fact]
    public async Task GivenCleanWorktree_WhenListingChanges_ThenEmpty()
    {
        using var repository = TestRepositories.WithManyWorktrees(out var lockedPath, out _, out _);

        var changes = await Writer.ListChangesAsync(lockedPath, Token);

        Assert.Empty(changes);
    }

    [Fact]
    public async Task GivenWorktree_WhenLockingWithReasonAndUnlocking_ThenListingFollows()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("lockable", "lockable-branch", createBranch: true);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Writer.LockAsync(location, path, "on a usb stick", Token);
        var locked = await ReadLockableAsync(location);
        await Writer.UnlockAsync(location, path, Token);
        var unlocked = await ReadLockableAsync(location);

        Assert.True(locked.IsLocked);
        Assert.Equal("on a usb stick", locked.LockReason);
        Assert.False(unlocked.IsLocked);
    }

    [Fact]
    public async Task GivenBlankReason_WhenLocking_ThenLockedWithoutReason()
    {
        using var repository = TestRepositories.Linear(2);
        var path = repository.AddWorktree("lockable", "lockable-branch", createBranch: true);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        await Writer.LockAsync(location, path, "  ", Token);

        var locked = await ReadLockableAsync(location);
        Assert.True(locked.IsLocked);
        Assert.Null(locked.LockReason);
    }

    [Fact]
    public async Task GivenPrunableAndLockedAndDirtyWorktrees_WhenPruning_ThenOnlyThePrunableEntryGoes()
    {
        using var repository =
            TestRepositories.WithManyWorktrees(out var lockedPath, out var prunablePath, out var dirtyPath);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        var removed = await Writer.PruneAsync(location, Token);

        var remaining = (await Refs.ReadWorktreesAsync(location, Token)).Items;
        Assert.DoesNotContain(remaining, worktree => worktree.IsPrunable);
        Assert.DoesNotContain(remaining, worktree => SamePath(worktree.Path, prunablePath));
        Assert.Contains(remaining, worktree => SamePath(worktree.Path, lockedPath) && worktree.IsLocked);
        Assert.Contains(remaining, worktree => SamePath(worktree.Path, dirtyPath));
        Assert.Single(removed);
        Assert.Contains("wt-prunable", removed[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNothingPrunable_WhenPruning_ThenNothingIsReported()
    {
        using var repository = TestRepositories.WithWorktree(out _);
        var location = await Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

        var removed = await Writer.PruneAsync(location, Token);

        Assert.Empty(removed);
    }

    // Git reports forward slashes on Windows.
    private static bool SamePath(string left, string right) => Path.GetFullPath(left) == Path.GetFullPath(right);

    private static async Task<WorktreeInfo> ReadLockableAsync(RepositoryLocation location)
    {
        var worktrees = await Refs.ReadWorktreesAsync(location, Token);
        return worktrees.Items.Single(item => item.BranchFullName == "refs/heads/lockable-branch");
    }

    private static string NewPath(TestRepository repository, string name) =>
        Path.Combine(Path.GetDirectoryName(repository.WorkingTreePath)!, name);
}
