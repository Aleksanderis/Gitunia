using Gitunia.App.Integration;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Integration;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Merge;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Integration;

// Real git on temporary repositories: the handler is the only place that runs the reset, merge and operation calls.
public class IntegrationEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenACommit_WhenResettingSoft_ThenTheBranchMovesTheChangeStaysStagedAndTheTextNamesTheUndo()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        var first = FirstCommit(repository);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ResetBranchEffect(Tab, location, Target(first, ResetMode.Soft)));

        var success = Assert.IsType<IntegrationSucceeded>(result);
        Assert.Equal(first, repository.Head);
        Assert.Equal("M  a.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Contains($"was {old[..7]}", success.Text, StringComparison.Ordinal);
        Assert.Contains($"git reset --soft {old}", success.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Info, success.Severity);
    }

    [Fact]
    public async Task GivenADirtyTree_WhenResettingHard_ThenTheFilesAreOverwrittenAndUntrackedOnesStay()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "dirty");
        repository.WriteFile("untracked.txt", "keep");
        var first = FirstCommit(repository);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ResetBranchEffect(Tab, location, Target(first, ResetMode.Hard)));

        Assert.Contains("git reset --hard", Assert.IsType<IntegrationSucceeded>(result).Text, StringComparison.Ordinal);
        Assert.Equal(first, repository.Head);
        Assert.Equal("1", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "untracked.txt")));
    }

    [Fact]
    public async Task GivenUnstagedChanges_WhenResettingMixed_ThenTheFilesAreKept()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "unstaged edit");
        var first = FirstCommit(repository);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ResetBranchEffect(Tab, location, Target(first, ResetMode.Mixed)));

        Assert.IsType<IntegrationSucceeded>(result);
        Assert.Equal(" M a.txt", repository.Git("status", "--porcelain").TrimEnd());
        Assert.Equal("unstaged edit", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "a.txt")));
    }

    [Fact]
    public async Task GivenAnUnknownCommit_WhenResetting_ThenTheErrorIsReportedAndNothingMoves()
    {
        using var repository = TwoCommits();
        var old = repository.Head;
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ResetBranchEffect(Tab, location, Target(new string('a', 40), ResetMode.Soft)));

        var failed = Assert.IsType<IntegrationFailed>(result);
        Assert.StartsWith("Could not reset:", failed.Text, StringComparison.Ordinal);
        Assert.True(failed.Refresh);
        Assert.Equal(old, repository.Head);
    }

    [Fact]
    public async Task GivenStagedAndUntrackedFiles_WhenPreparingHard_ThenOnlyTheTrackedChangesAreListed()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "staged edit");
        repository.Git("add", "a.txt");
        repository.WriteFile("untracked.txt", "x");
        var location = await LocateAsync(repository);

        var target = Target(FirstCommit(repository), ResetMode.Hard);

        var result = await RunAsync(new PrepareResetEffect(Tab, location, target));

        var prepared = Assert.IsType<ResetPrepared>(result);
        var entry = Assert.Single(prepared.Changes);
        Assert.Equal("a.txt", entry.Path);
        Assert.True(entry.IsStaged);
        Assert.Equal(RepositoryOperation.None, prepared.Operation);
    }

    [Fact]
    public async Task GivenADirtyTree_WhenPreparingSoft_ThenNoStatusIsRead()
    {
        using var repository = TwoCommits();
        repository.WriteFile("a.txt", "dirty");
        var location = await LocateAsync(repository);

        var target = Target(FirstCommit(repository), ResetMode.Soft);

        var result = await RunAsync(new PrepareResetEffect(Tab, location, target));

        Assert.Empty(Assert.IsType<ResetPrepared>(result).Changes);
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenPreparing_ThenTheOperationIsReported()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);

        var target = Target(FirstCommit(repository), ResetMode.Soft);

        var result = await RunAsync(new PrepareResetEffect(Tab, location, target));

        Assert.Equal(RepositoryOperation.Merge, Assert.IsType<ResetPrepared>(result).Operation);
    }

    [Fact]
    public async Task GivenAMissingRepository_WhenPreparingHard_ThenTheFailureSaysNothingWasResetAndSkipsTheRefresh()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gitunia-missing-{Guid.NewGuid():N}");
        var gitDirectory = Path.Combine(root, ".git");
        var location = new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);

        var result = await RunAsync(new PrepareResetEffect(Tab, location, Target(new string('a', 40), ResetMode.Hard)));

        var failed = Assert.IsType<IntegrationFailed>(result);
        Assert.StartsWith("Could not read the status, nothing was reset:", failed.Text, StringComparison.Ordinal);
        Assert.False(failed.Refresh);
    }

    [Fact]
    public async Task GivenABranchAhead_WhenMerging_ThenItFastForwardsAndNamesTheOldTipForTheUndo()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        var topic = repository.Git("rev-parse", "topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, "refs/heads/topic", "topic", MergeMode.Default));

        var text = Assert.IsType<IntegrationSucceeded>(result).Text;
        Assert.Equal(topic, repository.Head);
        Assert.StartsWith("Fast-forwarded main", text, StringComparison.Ordinal);
        Assert.Contains($"git reset --hard {old[..7]}", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACommit_WhenMergingWithNoFastForward_ThenAMergeCommitIsMade()
    {
        using var repository = AheadBranch();
        var topic = repository.Git("rev-parse", "topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, topic, topic[..7], MergeMode.NoFastForward));

        Assert.StartsWith("Merged ", Assert.IsType<IntegrationSucceeded>(result).Text, StringComparison.Ordinal);
        Assert.Equal(2, repository.Git("rev-list", "--parents", "-n", "1", "HEAD").Trim().Split(' ').Length - 1);
    }

    [Fact]
    public async Task GivenABranch_WhenSquashing_ThenTheChangesAreStagedAndTheTextSaysNothingIsCommitted()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, "refs/heads/topic", "topic", MergeMode.Squash));

        var text = Assert.IsType<IntegrationSucceeded>(result).Text;
        Assert.Contains("nothing is committed", text, StringComparison.Ordinal);
        Assert.Equal(old, repository.Head);
        Assert.Contains("A  b.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenATagWithTheNameOfTheBranch_WhenMergingTheBranchRef_ThenTheBranchIsMerged()
    {
        using var repository = AheadBranch();
        repository.Git("tag", "topic", "main");
        var topic = repository.Git("rev-parse", "refs/heads/topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, "refs/heads/topic", "topic", MergeMode.Default));

        Assert.IsType<IntegrationSucceeded>(result);
        Assert.Equal(topic, repository.Head);
    }

    [Theory]
    [InlineData(MergeMode.Default, "<space>gc")]
    [InlineData(MergeMode.Squash, "no operation is in progress")]
    public async Task GivenConflictingBranches_WhenMerging_ThenAWarningSaysHowToGoOn(MergeMode mode, string expected)
    {
        using var repository = TestRepositories.ConflictingBranches();
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, "refs/heads/other", "other", mode));

        var success = Assert.IsType<IntegrationSucceeded>(result);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains(expected, success.Text, StringComparison.Ordinal);
        Assert.Contains("conflict.txt", repository.Git("status", "--porcelain"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenADirtyTreeGitCannotMergeOver_WhenMerging_ThenGitsMessageIsTheFailure()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("a.txt", "topic"))
            .Checkout("main")
            .WriteFile("a.txt", "dirty");
        var location = await LocateAsync(repository);

        var result = await RunAsync(Merge(location, "refs/heads/topic", "topic", MergeMode.Default));

        var failed = Assert.IsType<IntegrationFailed>(result);
        Assert.StartsWith("Could not merge topic into main:", failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenACancelledToken_WhenMerging_ThenTheResultIsCancelledAndNothingMerges()
    {
        using var repository = AheadBranch();
        var old = repository.Head;
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var effect = Merge(location, "refs/heads/topic", "topic", MergeMode.Default);

        var result = await RunWithTokenAsync(effect, source.Token);

        Assert.IsType<IntegrationCancelled>(result);
        Assert.Equal(old, repository.Head);
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge, "Merged ")]
    [InlineData(RepositoryOperation.Rebase, "Rebased ")]
    [InlineData(RepositoryOperation.CherryPick, "Cherry-pick finished")]
    [InlineData(RepositoryOperation.Revert, "Revert finished")]
    public async Task GivenEachOperationStoppedOnAConflict_WhenResolvedAndContinued_ThenItFinishes(
        RepositoryOperation operation,
        string expectedStart)
    {
        using var repository = Fixture(operation);
        var location = await LocateAsync(repository);
        repository.WriteFile("conflict.txt", "resolved\n");
        repository.Git("add", "conflict.txt");

        var result = await RunAsync(new ContinueOperationEffect(Tab, location, await InfoAsync(location), "main"));

        var success = Assert.IsType<IntegrationSucceeded>(result);
        Assert.StartsWith(expectedStart, success.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Info, success.Severity);
        Assert.Equal(RepositoryOperation.None, Detect(location));
    }

    [Fact]
    public async Task GivenARebaseWithTwoConflicts_WhenContinuing_ThenItWarnsThatItStoppedAgain()
    {
        using var repository = TestRepository.Create()
            .Commit("Initial", new TestFile("conflict.txt", "original\n"))
            .CheckoutNewBranch("other")
            .Commit("First", new TestFile("conflict.txt", "first\n"))
            .Commit("Second", new TestFile("conflict.txt", "second\n"))
            .Checkout("main")
            .Commit("Main change", new TestFile("conflict.txt", "main\n"))
            .Checkout("other")
            .RebaseExpectingConflict("main");
        var location = await LocateAsync(repository);
        repository.WriteFile("conflict.txt", "resolved\n");
        repository.Git("add", "conflict.txt");

        var result = await RunAsync(new ContinueOperationEffect(Tab, location, await InfoAsync(location), "other"));

        var success = Assert.IsType<IntegrationSucceeded>(result);
        Assert.Equal(StatusSeverity.Warning, success.Severity);
        Assert.Contains("stopped on conflicts", success.Text, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.Rebase, Detect(location));
    }

    [Fact]
    public async Task GivenConflictsLeft_WhenContinuingAsGitRefuses_ThenTheFailureShowsGitsReason()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new ContinueOperationEffect(Tab, location, await InfoAsync(location), "main"));

        var failed = Assert.IsType<IntegrationFailed>(result);
        Assert.StartsWith("Could not continue the merge:", failed.Text, StringComparison.Ordinal);
        Assert.True(failed.Refresh);
    }

    [Theory]
    [InlineData(RepositoryOperation.Merge)]
    [InlineData(RepositoryOperation.Rebase)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    public async Task GivenEachOperationStoppedOnAConflict_WhenAborted_ThenTheOldTipReturns(
        RepositoryOperation operation)
    {
        using var repository = Fixture(operation);
        var rebased = operation == RepositoryOperation.Rebase;
        var before = rebased ? repository.Git("rev-parse", "other").Trim() : repository.Head;
        var location = await LocateAsync(repository);

        var result = await RunAsync(new AbortOperationEffect(Tab, location, await InfoAsync(location), "main"));

        Assert.StartsWith("Aborted the ", Assert.IsType<IntegrationSucceeded>(result).Text, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.None, Detect(location));
        Assert.Equal(before, repository.Head);
    }

    [Theory]
    [InlineData(RepositoryOperation.Rebase)]
    [InlineData(RepositoryOperation.CherryPick)]
    [InlineData(RepositoryOperation.Revert)]
    public async Task GivenEachSkippableOperationOnAConflict_WhenSkipped_ThenItFinishesWithoutTheCommit(
        RepositoryOperation operation)
    {
        using var repository = Fixture(operation);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new SkipOperationEffect(Tab, location, await InfoAsync(location)));

        Assert.StartsWith("Skipped ", Assert.IsType<IntegrationSucceeded>(result).Text, StringComparison.Ordinal);
        Assert.Equal(RepositoryOperation.None, Detect(location));
    }

    [Fact]
    public async Task GivenACancelledToken_WhenAborting_ThenTheResultIsCancelled()
    {
        using var repository = TestRepositories.Conflicted();
        var location = await LocateAsync(repository);
        var info = await InfoAsync(location);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var result = await RunWithTokenAsync(new AbortOperationEffect(Tab, location, info, "main"), source.Token);

        Assert.IsType<IntegrationCancelled>(result);
    }

    private static MergeBranchEffect Merge(
        RepositoryLocation location,
        string revision,
        string label,
        MergeMode mode) =>
        new(Tab, location, "main", new MergeTarget(revision, label), mode);

    private static ResetTarget Target(string commit, ResetMode mode) => new(commit, "main", mode);

    private static TestRepository TwoCommits() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"));

    private static TestRepository AheadBranch() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .CheckoutNewBranch("topic")
            .Commit("two", new TestFile("b.txt", "2"))
            .Checkout("main");

    private static string FirstCommit(TestRepository repository) =>
        repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();

    private static TestRepository Fixture(RepositoryOperation operation) => operation switch
    {
        RepositoryOperation.Merge => TestRepositories.Conflicted(),
        RepositoryOperation.Rebase => TestRepositories.RebaseConflicted(),
        RepositoryOperation.CherryPick => TestRepositories.CherryPickConflicted(),
        _ => TestRepositories.RevertConflicted(),
    };

    private static RepositoryOperation Detect(RepositoryLocation location) =>
        new RepositoryOperationDetector().Detect(location);

    private static async Task<OperationInfo> InfoAsync(RepositoryLocation location)
    {
        var info = await TestGit.Services.OperationInfo.ReadAsync(
            location,
            Detect(location),
            TestContext.Current.CancellationToken);

        return info!;
    }

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(IntegrationEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(
        IntegrationEffect effect,
        CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        IntegrationEffectHandler.Create(TestGit.Services, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
