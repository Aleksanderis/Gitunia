using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Stash;

public class StashWriterTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly StashReader Reader = new(Runner);
    private static readonly StashWriter Writer = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenTrackedChange_WhenPushingWithMessage_ThenStashHoldsItAndTreeIsClean()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("file.txt", "edited\n");
        var location = await LocateAsync(repository);

        var result = await Writer.PushAsync(location, "my work", false, Token);

        Assert.Equal(StashOutcome.Done, result.Outcome);
        Assert.Equal(repository.Git("rev-parse", "stash@{0}").Trim(), result.CommitHash);
        var entry = Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
        Assert.Contains("my work", entry.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, repository.Git("status", "--porcelain").Trim());
    }

    [Fact]
    public async Task GivenOnlyUntrackedFile_WhenPushingWithoutUntracked_ThenNothingIsStashedAndFileStays()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("new.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await Writer.PushAsync(location, string.Empty, false, Token);

        Assert.Equal(StashOutcome.NothingToStash, result.Outcome);
        Assert.Empty((await Reader.ReadStashesAsync(location, Token)).Items);
        Assert.True(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
    }

    [Fact]
    public async Task GivenUntrackedFileAndOldStash_WhenPushingWithoutUntracked_ThenTheOldTopIsNotTakenForANewStash()
    {
        using var repository = TestRepositories.WithStash();
        repository.WriteFile("another.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await Writer.PushAsync(location, string.Empty, false, Token);

        Assert.Equal(StashOutcome.NothingToStash, result.Outcome);
        Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
    }

    [Fact]
    public async Task GivenUntrackedFile_WhenPushingWithUntracked_ThenFileIsStashedAndRemoved()
    {
        using var repository = TestRepositories.Linear(2);
        repository.WriteFile("new.txt", "x\n");
        var location = await LocateAsync(repository);

        var result = await Writer.PushAsync(location, string.Empty, true, Token);

        Assert.Equal(StashOutcome.Done, result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "new.txt")));
    }

    [Fact]
    public async Task GivenStash_WhenApplying_ThenChangesReturnAndStashStays()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var result = await Writer.ApplyAsync(location, entry, Token);

        Assert.Equal(StashOutcome.Done, result.Outcome);
        Assert.Equal("stashed change\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
        Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
    }

    [Fact]
    public async Task GivenStash_WhenPopping_ThenChangesReturnAndStashIsGone()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var result = await Writer.PopAsync(location, entry, Token);

        Assert.Equal(StashOutcome.Done, result.Outcome);
        Assert.Equal("stashed change\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
        Assert.Empty((await Reader.ReadStashesAsync(location, Token)).Items);
    }

    [Fact]
    public async Task GivenConflictingStash_WhenPopping_ThenOutcomeIsConflictedAndStashIsKept()
    {
        using var repository = TestRepositories.WithStash();
        repository.Commit("conflicting", new TestFile("file.txt", "other change\n"));
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        var result = await Writer.PopAsync(location, entry, Token);

        Assert.Equal(StashOutcome.Conflicted, result.Outcome);
        Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
        Assert.NotEqual(string.Empty, repository.Git("ls-files", "--unmerged").Trim());
    }

    [Fact]
    public async Task GivenLocalChangeInTheWay_WhenApplying_ThenGitsRefusalIsThrownAndNothingChanges()
    {
        using var repository = TestRepositories.WithStash();
        repository.WriteFile("file.txt", "local edit\n");
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => Writer.ApplyAsync(location, entry, Token));

        Assert.Equal("local edit\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "file.txt")));
        Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
    }

    [Fact]
    public async Task GivenMergeStoppedOnConflicts_WhenApplying_ThenGitsRefusalIsThrownNotAConflict()
    {
        using var repository = TestRepositories.WithStash();
        var main = repository.Git("branch", "--show-current").Trim();
        repository.CheckoutNewBranch("side").Commit("side", new TestFile("file.txt", "side\n"));
        repository.Checkout(main).Commit("main", new TestFile("file.txt", "main\n"));
        repository.MergeExpectingConflict("side");
        Assert.NotEqual(string.Empty, repository.Git("ls-files", "--unmerged").Trim());
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => Writer.ApplyAsync(location, entry, Token));
    }

    [Fact]
    public async Task GivenTwoStashes_WhenDroppingAfterAnotherPush_ThenTheStashOfTheEntryGoesNotTheOneNowAtItsIndex()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var old = await TopAsync(location);
        repository.WriteFile("file.txt", "newer\n");
        repository.Git("stash", "push", "-q", "-m", "newer");

        var result = await Writer.DropAsync(location, old, Token);

        Assert.Equal(StashOutcome.Done, result.Outcome);
        var remaining = Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
        Assert.Contains("newer", remaining.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenDroppedStash_WhenRestoringWithTheHint_ThenItIsBackWithItsSubject()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);

        await Writer.DropAsync(location, entry, Token);
        Assert.Empty((await Reader.ReadStashesAsync(location, Token)).Items);
        repository.Git("stash", "store", "-m", entry.Message, entry.CommitHash);

        var restored = Assert.Single((await Reader.ReadStashesAsync(location, Token)).Items);
        Assert.Equal(entry.CommitHash, restored.CommitHash);
        Assert.Equal(entry.Message, restored.Message);
    }

    [Fact]
    public async Task GivenEntryAlreadyGone_WhenPoppingOrDropping_ThenOutcomeIsMissingAndNothingChanges()
    {
        using var repository = TestRepositories.WithStash();
        var location = await LocateAsync(repository);
        var entry = await TopAsync(location);
        repository.Git("stash", "drop", "-q");

        var popped = await Writer.PopAsync(location, entry, Token);
        var dropped = await Writer.DropAsync(location, entry, Token);

        Assert.Equal(StashOutcome.Missing, popped.Outcome);
        Assert.Equal(StashOutcome.Missing, dropped.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "untracked.txt")));
    }

    private static Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        Discovery.DiscoverAsync(repository.WorkingTreePath, Token);

    private static async Task<StashEntry> TopAsync(RepositoryLocation location) =>
        (await Reader.ReadStashesAsync(location, Token)).Items[0];
}
