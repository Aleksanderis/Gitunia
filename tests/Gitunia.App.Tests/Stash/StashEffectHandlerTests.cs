using Gitunia.App.Stash;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Stash;

// Real git on temporary repositories: the handler is the only place that runs the stash commands.
public class StashEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenTrackedChanges_WhenPushingWithAMessage_ThenGitHasTheStashAndTheTextNamesItAndTheHash()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("file.txt", "changed\n");
        repository.WriteFile("untracked.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushStashEffect(Tab, location, "my work", false));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.StartsWith("Stashed \"my work\" as ", success.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Info, success.Severity);
        Assert.Contains("my work", repository.Git("stash", "list"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "untracked.txt")));
    }

    [Fact]
    public async Task GivenAnUntrackedFile_WhenPushingWithTheUntrackedKey_ThenTheFileIsStashedToo()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("untracked.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushStashEffect(Tab, location, string.Empty, true));

        Assert.IsType<StashOperationSucceeded>(result);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "untracked.txt")));
    }

    [Fact]
    public async Task GivenNothingToStash_WhenPushingWithoutUntracked_ThenTheWarningPointsAtTheUntrackedKey()
    {
        using var repository = TestRepositories.Linear();
        repository.WriteFile("untracked.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await RunAsync(new PushStashEffect(Tab, location, string.Empty, false));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Equal("Nothing to stash (untracked files are stashed with <space>ku)", success.Text);
        Assert.Equal(string.Empty, repository.Git("stash", "list").Trim());
    }

    [Fact]
    public async Task GivenAStash_WhenApplying_ThenTheChangesAreBackAndTheStashIsKept()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);

        var result = await RunAsync(new ApplyStashEffect(Tab, location, entry, false));

        Assert.Equal("Applied stash@{0}; the stash is kept", Assert.IsType<StashOperationSucceeded>(result).Text);
        Assert.Equal("stashed change\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
        Assert.Contains("stash@{0}", repository.Git("stash", "list"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAStash_WhenPopping_ThenTheStashIsGoneAndTheTextCarriesTheRestoreCommand()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);

        var result = await RunAsync(new ApplyStashEffect(Tab, location, entry, true));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.Equal("Popped stash@{0}", success.Text);
        Assert.StartsWith("Restore: git stash store -m", success.Detail, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("stash", "list").Trim());
    }

    [Fact]
    public async Task GivenALocalChangeThatBlocksIt_WhenApplying_ThenTheFailureShowsGitsReason()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);
        repository.WriteFile("untracked.txt", "in the way\n");

        var result = await RunAsync(new ApplyStashEffect(Tab, location, entry, false));

        var failure = Assert.IsType<StashOperationFailed>(result);
        Assert.StartsWith("Could not apply the stash: ", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAStash_WhenDropping_ThenItIsGoneAndTheTextHasHashAndRestoreCommand()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);

        var result = await RunAsync(new DropStashEffect(Tab, location, entry));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.Equal($"Dropped stash@{{0}} ({entry.CommitHash[..7]})", success.Text);
        Assert.StartsWith("Restore: git stash store -m", success.Detail, StringComparison.Ordinal);
        Assert.EndsWith(entry.CommitHash, success.Detail, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("stash", "list").Trim());
    }

    [Fact]
    public async Task GivenTheEntryIsGone_WhenDropping_ThenItWarnsAndSaysNothingChanged()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);
        repository.Git("stash", "drop", "-q");

        var result = await RunAsync(new DropStashEffect(Tab, location, entry));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Equal("stash@{0} is no longer in the stash list: nothing was changed", success.Text);
    }

    [Fact]
    public async Task GivenAFileWithLocalChanges_WhenChecking_ThenTheStateIsOverwritesAndNothingIsWritten()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var target = await FileTargetAsync(location, "file.txt");
        repository.WriteFile("file.txt", "local edit\n");

        var result = await RunAsync(new CheckStashRestoreEffect(Tab, location, target));

        var checkedRestore = Assert.IsType<StashRestoreChecked>(result);
        Assert.Equal(StashRestoreState.Overwrites, checkedRestore.State);
        Assert.Equal(target, checkedRestore.Target);
        Assert.Equal("local edit\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
    }

    [Fact]
    public async Task GivenAFileWithLocalChanges_WhenRestoring_ThenTheStashedVersionIsBackAndTheTextHasTheUndo()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var target = await FileTargetAsync(location, "file.txt");
        repository.WriteFile("file.txt", "local edit\n");

        var result = await RunAsync(new RestoreStashFileEffect(Tab, location, target));

        var success = Assert.IsType<StashOperationSucceeded>(result);
        Assert.Equal("Restored file.txt from stash@{0}", success.Text);
        Assert.StartsWith("Undo: git cat-file blob ", success.Detail, StringComparison.Ordinal);
        Assert.Equal("stashed change\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRunning_ThenTheResultIsCancelledAndNothingChanges()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await FirstEntryAsync(location);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var result = await RunWithTokenAsync(new DropStashEffect(Tab, location, entry), source.Token);

        Assert.IsType<StashOperationCancelled>(result);
        Assert.Contains("stash@{0}", repository.Git("stash", "list"), StringComparison.Ordinal);
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static async Task<StashEntry> FirstEntryAsync(RepositoryLocation location) =>
        (await TestGit.Services.Stashes.ReadStashesAsync(location, TestContext.Current.CancellationToken)).Items[0];

    private static async Task<StashFileTarget> FileTargetAsync(RepositoryLocation location, string path)
    {
        var entry = await FirstEntryAsync(location);
        var change = new CommitFileChange(path, null, FileChangeKind.Modified, null);

        return new StashFileTarget(entry, new StashFileChange(change, StashFileOrigin.Tracked));
    }

    private static Task<AppCommand> RunAsync(StashEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(StashEffect effect, CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new StashEffectHandler(TestGit.Services.StashWriter, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
