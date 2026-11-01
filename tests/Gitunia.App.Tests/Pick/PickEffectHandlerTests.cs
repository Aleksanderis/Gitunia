using System.Collections.Immutable;
using Gitunia.App.Pick;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Pick;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories;
using Gitunia.Git.Pick;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Pick;

// Real git on temporary repositories: the handler is the only place that runs the pick commands.
public class PickEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenCommits_WhenReading_ThenTheSubjectsAndTheParentsOfAMergeCommitComeBack()
    {
        using var repository = MergedTopic();
        var merge = repository.Head;
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ReadPickCommitsEffect(Tab, location, PickKind.Revert, [merge]));

        var read = Assert.IsType<PickCommitsRead>(result);
        var commit = Assert.Single(read.Commits);
        Assert.True(commit.IsMerge);
        Assert.Equal(["main two", "topic two"], commit.Parents.Select(parent => parent.Subject));
        Assert.Equal(PickKind.Revert, read.PickKind);
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenReading_ThenTheReadFailsWithNothingDone()
    {
        using var repository = Diverged();
        var location = await LocateAsync(repository);

        var effect = new ReadPickCommitsEffect(Tab, location, PickKind.CherryPick, [new string('a', 40)]);

        var result = await RunAsync(effect);

        var failed = Assert.IsType<PickReadFailed>(result);
        Assert.StartsWith("Could not read the commits, nothing was done: ", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommit_WhenCherryPicking_ThenItLandsOnHeadAndTheResultNamesTheOldTip()
    {
        using var repository = Diverged();
        var old = repository.Head;
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.CherryPick, Hash(repository, "topic"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"));

        var finished = Assert.IsType<PickFinished>(result);
        Assert.Equal(PickOutcome.Completed, finished.Result.Outcome);
        Assert.Equal(old, finished.Result.OldHead);
        Assert.Equal("main", finished.Target);
        Assert.StartsWith(Hash(repository, "topic")[..7], finished.What, StringComparison.Ordinal);
        Assert.Equal("topic two", repository.Git("log", "-1", "--format=%s").Trim());
    }

    [Fact]
    public async Task GivenARangeOldestFirst_WhenCherryPicking_ThenBothLandInOrder()
    {
        using var repository = Diverged().Checkout("topic")
            .Commit("topic three", new TestFile("c.txt", "3"))
            .Checkout("main");
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(
            location,
            PickKind.CherryPick,
            Hash(repository, "topic~1"),
            Hash(repository, "topic"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"));

        var finished = Assert.IsType<PickFinished>(result);
        Assert.Equal("2 commits", finished.What);
        Assert.Equal("topic three\ntopic two", repository.Git("log", "-2", "--format=%s").Trim().Replace("\r", ""));
    }

    [Fact]
    public async Task GivenARangeNewestFirst_WhenReverting_ThenBothAreUndone()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("b.txt", "2"))
            .Commit("three", new TestFile("c.txt", "3"));
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.Revert, Hash(repository, "HEAD"), Hash(repository, "HEAD~1"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.Revert, commits, null, "main"));

        Assert.Equal(PickOutcome.Completed, Assert.IsType<PickFinished>(result).Result.Outcome);
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
        Assert.False(File.Exists(Path.Combine(repository.WorkingTreePath, "c.txt")));
    }

    [Fact]
    public async Task GivenACommitHeadAlreadyHas_WhenCherryPicking_ThenThereIsNothingToApply()
    {
        using var repository = Diverged();
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.CherryPick, Hash(repository, "main~1"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"));

        Assert.Equal(PickOutcome.NothingToApply, Assert.IsType<PickFinished>(result).Result.Outcome);
    }

    [Fact]
    public async Task GivenAConflictingCommit_WhenCherryPicking_ThenTheOperationIsLeftInProgress()
    {
        using var repository = TestRepositories.ConflictingBranches();
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.CherryPick, Hash(repository, "other"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"));

        Assert.Equal(PickOutcome.Conflicted, Assert.IsType<PickFinished>(result).Result.Outcome);
        Assert.Contains("UU conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.CherryPick, new RepositoryOperationDetector().Detect(location));
    }

    [Fact]
    public async Task GivenAConflictingRevert_WhenReverting_ThenTheRevertIsLeftInProgress()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .Commit("First change", new TestFile("conflict.txt", "first\n"))
            .Commit("Second change", new TestFile("conflict.txt", "second\n"));
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.Revert, Hash(repository, "HEAD~1"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.Revert, commits, null, "main"));

        Assert.Equal(PickOutcome.Conflicted, Assert.IsType<PickFinished>(result).Result.Outcome);
        Assert.Equal(RepositoryOperation.Revert, new RepositoryOperationDetector().Detect(location));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task GivenAMergeCommit_WhenRevertingWithAMainline_ThenThatSideIsKept(int parent, bool mainSideKept)
    {
        using var repository = MergedTopic();
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.Revert, repository.Head);

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.Revert, commits, parent, "main"));

        Assert.Equal(PickOutcome.Completed, Assert.IsType<PickFinished>(result).Result.Outcome);
        Assert.Equal(mainSideKept, File.Exists(Path.Combine(repository.WorkingTreePath, "m.txt")));
        Assert.Equal(!mainSideKept, File.Exists(Path.Combine(repository.WorkingTreePath, "b.txt")));
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotPickOver_WhenCherryPicking_ThenGitsMessageComesBackAsTheFailure()
    {
        using var repository = Diverged().WriteFile("b.txt", "dirty");
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.CherryPick, Hash(repository, "topic"));

        var result = await RunAsync(new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"));

        var failed = Assert.IsType<PickFailed>(result);
        Assert.StartsWith("Could not cherry-pick ", failed.Text, StringComparison.Ordinal);
        Assert.Contains(" onto main: ", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenRunning_ThenNothingChangesAndTheResultIsACancel()
    {
        using var repository = Diverged();
        var old = repository.Head;
        var location = await LocateAsync(repository);
        var commits = await ReadAsync(location, PickKind.CherryPick, Hash(repository, "topic"));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(
            new RunPickEffect(Tab, location, PickKind.CherryPick, commits, null, "main"),
            cancelled.Token);

        Assert.IsType<PickCancelled>(result);
        Assert.Equal(old, repository.Head);
    }

    // Main has "main two" (m.txt), topic has "topic two" (b.txt), both forked from "one".
    private static TestRepository Diverged() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"));

    // Main gets m.txt and merges topic (b.txt), so HEAD is a merge commit.
    private static TestRepository MergedTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("topic two", new TestFile("b.txt", "2"))
            .Checkout("main")
            .Commit("main two", new TestFile("m.txt", "m"))
            .Merge("topic");

    private static string Hash(TestRepository repository, string revision) =>
        repository.Git("rev-parse", revision).Trim();

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static async Task<ImmutableArray<PickCommit>> ReadAsync(
        RepositoryLocation location,
        PickKind kind,
        params string[] hashes)
    {
        var result = await RunAsync(new ReadPickCommitsEffect(Tab, location, kind, [.. hashes]));

        return Assert.IsType<PickCommitsRead>(result).Commits;
    }

    private static Task<AppCommand> RunAsync(PickEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(PickEffect effect, CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new PickEffectHandler(TestGit.Services.Pick, TestGit.Services.PickCommits, dispatcher)
            .Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
