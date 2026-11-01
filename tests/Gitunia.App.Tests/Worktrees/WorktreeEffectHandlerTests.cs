using Gitunia.App.Tests.Tags;
using Gitunia.App.Worktrees;
using Gitunia.Core;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Worktrees.Models;
using Xunit;

namespace Gitunia.App.Tests.Worktrees;

// Real git on temporary repositories: the handler is the only place that runs the worktree commands.
public class WorktreeEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenANewBranchName_WhenAdding_ThenTheWorktreeExistsOnTheNewBranchAndTheTextSaysSo()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var location = await LocateAsync(repository.WorkingTreePath);
        var path = Path.Combine(repository.WorkingTreePath, "..", "added");
        var request = new WorktreeAddRequest(Path.GetFullPath(path), "fresh", repository.Head);

        var result = await RunAsync(new AddWorktreeEffect(Tab, location, request, false));

        var success = Assert.IsType<WorktreeOperationSucceeded>(result);
        Assert.Equal($"Added worktree {request.Path} on fresh", success.Text);
        Assert.Null(success.OpenPath);
        Assert.Equal("fresh", repository.GitIn(request.Path, ["branch", "--show-current"]).Trim());
    }

    [Fact]
    public async Task GivenAnExistingFreeBranchAndOpenAfter_WhenAdding_ThenTheResultNamesThePathToOpen()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1")).Branch("topic");
        var location = await LocateAsync(repository.WorkingTreePath);
        var path = Path.GetFullPath(Path.Combine(repository.WorkingTreePath, "..", "topic-tree"));

        var result = await RunAsync(new AddWorktreeEffect(Tab, location, new WorktreeAddRequest(path, "topic", null), true));

        Assert.Equal(path, Assert.IsType<WorktreeOperationSucceeded>(result).OpenPath);
        Assert.Equal("topic", repository.GitIn(path, ["branch", "--show-current"]).Trim());
    }

    [Fact]
    public async Task GivenAPathThatExistsAndIsNotEmpty_WhenAdding_ThenGitsMessageIsTheFailureAndNothingIsCreated()
    {
        using var repository = TestRepository.Create().Commit("one", new TestFile("a.txt", "1"));
        var location = await LocateAsync(repository.WorkingTreePath);
        var occupied = Path.GetFullPath(Path.Combine(repository.WorkingTreePath, "..", "occupied"));
        Directory.CreateDirectory(occupied);
        await File.WriteAllTextAsync(Path.Combine(occupied, "x.txt"), "x", Token);

        var result = await RunAsync(
            new AddWorktreeEffect(Tab, location, new WorktreeAddRequest(occupied, "fresh", repository.Head), false));

        var failure = Assert.IsType<WorktreeOperationFailed>(result);
        Assert.StartsWith("Could not add the worktree: ", failure.Text, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(occupied, ".git")));
    }

    [Fact]
    public async Task GivenACleanWorktree_WhenRemoving_ThenTheDirectoryIsGoneAndTheBranchKept()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var location = await LocateAsync(repository.WorkingTreePath);

        var result = await RunAsync(new RemoveWorktreeEffect(Tab, location, path, false));

        Assert.Equal($"Removed worktree {path}", Assert.IsType<WorktreeOperationSucceeded>(result).Text);
        Assert.False(Directory.Exists(path));
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wt-branch").Trim());
    }

    [Fact]
    public async Task GivenADirtyWorktree_WhenRemovingWithoutForce_ThenTheChangesAreReportedAndNothingIsRemoved()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        await File.WriteAllTextAsync(Path.Combine(path, "dirty.txt"), "x", Token);
        var location = await LocateAsync(repository.WorkingTreePath);

        var result = await RunAsync(new RemoveWorktreeEffect(Tab, location, path, false));

        var needsForce = Assert.IsType<WorktreeRemoveNeedsForce>(result);
        Assert.Equal(path, needsForce.Path);
        Assert.Contains("dirty.txt", needsForce.Changes);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenADirtyWorktree_WhenRemovingWithForce_ThenItIsRemovedWithItsChanges()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        await File.WriteAllTextAsync(Path.Combine(path, "dirty.txt"), "x", Token);
        var location = await LocateAsync(repository.WorkingTreePath);

        var result = await RunAsync(new RemoveWorktreeEffect(Tab, location, path, true));

        Assert.Equal(
            $"Removed worktree {path} and its uncommitted changes",
            Assert.IsType<WorktreeOperationSucceeded>(result).Text);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenAWorktree_WhenLockingWithAReasonThenUnlocking_ThenTheListingFollows()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var location = await LocateAsync(repository.WorkingTreePath);

        var locked = await RunAsync(new LockWorktreeEffect(Tab, location, path, "usb"));
        var listedLocked = repository.Git("worktree", "list", "--porcelain");
        var unlocked = await RunAsync(new UnlockWorktreeEffect(Tab, location, path));
        var listedUnlocked = repository.Git("worktree", "list", "--porcelain");

        Assert.Equal($"Locked worktree {path} (usb)", Assert.IsType<WorktreeOperationSucceeded>(locked).Text);
        Assert.Contains("locked usb", listedLocked, StringComparison.Ordinal);
        Assert.Equal($"Unlocked worktree {path}", Assert.IsType<WorktreeOperationSucceeded>(unlocked).Text);
        Assert.DoesNotContain("locked", listedUnlocked, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoReason_WhenLocking_ThenTheTextHasNoParentheses()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var location = await LocateAsync(repository.WorkingTreePath);

        var result = await RunAsync(new LockWorktreeEffect(Tab, location, path, string.Empty));

        Assert.Equal($"Locked worktree {path}", Assert.IsType<WorktreeOperationSucceeded>(result).Text);
    }

    [Fact]
    public async Task GivenAPrunableAndALockedPrunableEntry_WhenPruning_ThenOnlyTheUnlockedEntryGoes()
    {
        using var repository = TestRepositories.WithManyWorktrees(out var lockedPath, out var prunablePath, out _);
        Directory.Delete(lockedPath, recursive: true);
        var location = await LocateAsync(repository.WorkingTreePath);

        var result = await RunAsync(new PruneWorktreesEffect(Tab, location));

        Assert.Equal("Pruned 1 worktree entry", Assert.IsType<WorktreeOperationSucceeded>(result).Text);
        var listed = repository.Git("worktree", "list", "--porcelain");
        Assert.DoesNotContain(prunablePath, listed, StringComparison.Ordinal);
        Assert.Contains(lockedPath, listed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRemoving_ThenTheResultIsCancelledAndTheDirectoryStays()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var location = await LocateAsync(repository.WorkingTreePath);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(new RemoveWorktreeEffect(Tab, location, path, false), cancelled.Token);

        Assert.IsType<WorktreeOperationCancelled>(result);
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task GivenAWorktreePath_WhenLocatingToSwitch_ThenTheResultCarriesItsLocationAndTheMode()
    {
        using var repository = TestRepositories.WithWorktree(out var path);
        var dispatcher = new RecordingDispatcher();

        new WorktreeEffectHandler(TestGit.Services.Worktrees, TestGit.Services.Discovery, dispatcher)
            .Locate(new LocateWorktreeEffect(Tab, path, true), Token);
        var result = await dispatcher.NextAsync(Token);

        var located = Assert.IsType<WorktreeLocated>(result);
        Assert.True(located.IsSwitch);
        Assert.Equal(Path.GetFullPath(path), Path.GetFullPath(located.Location.RootPath));
    }

    [Fact]
    public async Task GivenAPathThatIsNoRepository_WhenLocatingToOpen_ThenTheFailureNamesTheVerbAndThePath()
    {
        var missing = Path.Combine(Path.GetTempPath(), "gitunia-no-such-worktree", Guid.NewGuid().ToString("N"));
        var dispatcher = new RecordingDispatcher();

        new WorktreeEffectHandler(TestGit.Services.Worktrees, TestGit.Services.Discovery, dispatcher)
            .Locate(new LocateWorktreeEffect(Tab, missing, false), Token);
        var result = await dispatcher.NextAsync(Token);

        var failed = Assert.IsType<WorktreeLocateFailed>(result);
        Assert.StartsWith($"Could not open {missing}: ", failed.Text, StringComparison.Ordinal);
    }

    private static async Task<RepositoryLocation> LocateAsync(string path) =>
        await TestGit.Services.Discovery.DiscoverAsync(path, Token);

    private static Task<AppCommand> RunAsync(WorktreeEffect effect) => RunWithTokenAsync(effect, Token);

    private static async Task<AppCommand> RunWithTokenAsync(WorktreeEffect effect, CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new WorktreeEffectHandler(TestGit.Services.Worktrees, TestGit.Services.Discovery, dispatcher)
            .Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(Token);
    }
}
