using Gitunia.App.Branches;
using Gitunia.App.Tests.Tags;
using Gitunia.Core;
using Gitunia.Core.Branches;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Branches;

// Real git on temporary repositories: the handler is the only place that runs the branch commands.
public class BranchEffectHandlerTests
{
    private static readonly TabId Tab = new(1);

    [Fact]
    public async Task GivenABranchRowStartPoint_WhenCreatingWithCheckout_ThenTheBranchStartsThereAndIsCheckedOut()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);
        var effect = new CreateBranchEffect(
            Tab,
            location,
            "feature/new",
            "refs/heads/topic",
            "topic",
            true,
            repository.Head);

        var result = await RunAsync(effect);

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal("Created branch feature/new at topic and checked it out", success.Text);
        Assert.Equal(repository.Head, success.Follow);
        Assert.Equal("feature/new", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "topic").Trim());
    }

    [Fact]
    public async Task GivenACommit_WhenCreatingWithoutCheckout_ThenHeadStaysAndTheBranchIsAtTheCommit()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new CreateBranchEffect(Tab, location, "old", first, "abc1234", false, first));

        Assert.Equal("Created branch old at abc1234", Assert.IsType<BranchOperationSucceeded>(result).Text);
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(first, repository.Git("rev-parse", "old").Trim());
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenCreating_ThenNothingIsCreatedAndTheFailureNamesIt()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);

        var effect = new CreateBranchEffect(Tab, location, "bad name", repository.Head, "main", true, null);

        var result = await RunAsync(effect);

        var failure = Assert.IsType<BranchOperationFailed>(result);
        Assert.Equal("Could not create the branch: 'bad name' is not a valid branch name", failure.Text);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "bad name").Trim());
    }

    [Fact]
    public async Task GivenALocalBranch_WhenSwitching_ThenItIsCheckedOut()
    {
        using var repository = RepositoryWithTopic().Branch("release");
        var location = await LocateAsync(repository);

        var result = await RunAsync(SwitchTo(location, SwitchTargetKind.LocalBranch, "release"));

        Assert.Equal("Switched to release", Assert.IsType<BranchOperationSucceeded>(result).Text);
        Assert.Equal("release", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenARemoteBranchWithoutLocalTwin_WhenSwitching_ThenATrackingBranchIsCreated()
    {
        using var repository = RepositoryWithTopic();
        repository.AddOrigin();
        repository.Git("push", "-q", "origin", "topic");
        repository.Git("branch", "-D", "topic");
        repository.Git("fetch", "-q", "origin");
        var location = await LocateAsync(repository);

        var result = await RunAsync(SwitchTo(location, SwitchTargetKind.RemoteBranch, "origin/topic"));

        Assert.Equal(
            "Switched to new branch topic tracking origin/topic",
            Assert.IsType<BranchOperationSucceeded>(result).Text);
        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("origin/topic", repository.Git("rev-parse", "--abbrev-ref", "topic@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenACommit_WhenSwitchingToTheDetachedEntry_ThenHeadIsDetachedThere()
    {
        using var repository = RepositoryWithTopic();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(SwitchTo(location, SwitchTargetKind.DetachedCommit, first));

        Assert.Equal($"HEAD is detached at {first[..7]}", Assert.IsType<BranchOperationSucceeded>(result).Text);
        Assert.Equal(first, repository.Head);
        Assert.Equal(string.Empty, repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenADirtyTreeThatConflicts_WhenSwitching_ThenGitsMessageIsShownAndTheEditSurvives()
    {
        using var repository = TestRepository.Create()
            .Commit("one", new TestFile("shared.txt", "1\n"))
            .CheckoutNewBranch("topic")
            .Commit("topic", new TestFile("shared.txt", "topic\n"))
            .Checkout("main");
        repository.WriteFile("shared.txt", "local edit\n");
        var location = await LocateAsync(repository);

        var result = await RunAsync(SwitchTo(location, SwitchTargetKind.LocalBranch, "topic"));

        var failure = Assert.IsType<BranchOperationFailed>(result);
        Assert.StartsWith("Could not switch: ", failure.Text, StringComparison.Ordinal);
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("local edit\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "shared.txt")));
    }

    [Fact]
    public async Task GivenAFailingDetachedCheckout_WhenSwitching_ThenTheFailureSaysCouldNotCheckOut()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);

        var result = await RunAsync(SwitchTo(location, SwitchTargetKind.DetachedCommit, new string('f', 40)));

        var failure = Assert.IsType<BranchOperationFailed>(result);
        Assert.StartsWith("Could not check out: ", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenABranch_WhenRenaming_ThenTheNameMovesAndTheCursorTargetIsKept()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RenameBranchEffect(Tab, location, "topic", "topic-2", repository.Head));

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal("Renamed topic to topic-2", success.Text);
        Assert.Equal(repository.Head, success.Follow);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic-2").Trim());
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenAnInvalidName_WhenRenaming_ThenTheBranchKeepsItsName()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new RenameBranchEffect(Tab, location, "topic", "bad name", null));

        Assert.Equal(
            "Could not rename the branch: 'bad name' is not a valid branch name",
            Assert.IsType<BranchOperationFailed>(result).Text);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenAMergedBranch_WhenDeleting_ThenItIsGoneAndTheTextSaysHowToRestoreIt()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);

        var result = await RunAsync(Delete(location, "topic", repository.Head, force: false));

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal($"Deleted branch topic (was {repository.Head[..7]})", success.Text);
        Assert.Equal($"Restore: git branch topic {repository.Head}", success.Detail);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenDeletingWithoutForce_ThenItStaysAndTheSecondAnswerIsAsked()
    {
        using var repository = Unmerged(out var tip);
        var location = await LocateAsync(repository);

        var result = await RunAsync(Delete(location, "wip", tip, force: false));

        var needsForce = Assert.IsType<BranchDeleteNeedsForce>(result);
        Assert.Equal("wip", needsForce.Name);
        Assert.Equal(tip, needsForce.CommitHash);
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "wip").Trim());
    }

    [Fact]
    public async Task GivenAnUnmergedBranch_WhenDeletingWithForce_ThenItIsGoneAndTheTipIsReported()
    {
        using var repository = Unmerged(out var tip);
        var location = await LocateAsync(repository);

        var result = await RunAsync(Delete(location, "wip", tip, force: true));

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Contains(tip[..7], success.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(tip, success.Text, StringComparison.Ordinal);
        Assert.Equal($"Restore: git branch wip {tip}", success.Detail);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "wip").Trim());
        Assert.Equal(tip, repository.Git("rev-parse", tip).Trim());
    }

    [Fact]
    public async Task GivenAnUnmergedBranchWithAnUpstreamThatHasIt_WhenDeletingWithoutForce_ThenItIsDeleted()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.Git("switch", "main");
        var location = await LocateAsync(repository);
        var tip = repository.Git("rev-parse", "topic").Trim();

        var effect = Delete(location, "topic", tip, force: false, upstream: "refs/remotes/origin/topic");

        var result = await RunAsync(effect);

        Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenARemoteBranch_WhenSettingTheUpstream_ThenTheLocalBranchTracksIt()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var location = await LocateAsync(repository);

        var result = await RunAsync(new SetUpstreamEffect(Tab, location, "lonely", "origin/main"));

        Assert.Equal("lonely now tracks origin/main", Assert.IsType<BranchOperationSucceeded>(result).Text);
        Assert.Equal("origin/main", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenATrackingBranch_WhenUnsettingTheUpstream_ThenTheConfigIsGoneAndTheRestoreCommandIsShown()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        var result = await RunAsync(new UnsetUpstreamEffect(Tab, location, "topic", "origin/topic"));

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal("topic no longer tracks origin/topic", success.Text);
        Assert.Equal("Restore: git branch --set-upstream-to=origin/topic topic", success.Detail);
        Assert.Equal(string.Empty, repository.Git("for-each-ref", "--format=%(upstream)", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenABranchBehindItsUpstream_WhenFastForwarding_ThenItMovesAndTheUndoIsShown()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 2);
        repository.Git("switch", "main");
        repository.Git("fetch", "-q");
        var old = repository.Git("rev-parse", "refs/heads/topic").Trim();
        var tip = repository.Git("rev-parse", "refs/remotes/origin/topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(
            new FastForwardBranchEffect(Tab, location, "topic", "refs/remotes/origin/topic", old, tip));

        var success = Assert.IsType<BranchOperationSucceeded>(result);
        Assert.Equal($"Fast-forwarded topic from {old[..7]} to {tip[..7]}", success.Text);
        Assert.Equal($"Undo: git update-ref refs/heads/topic {old}", success.Detail);
        Assert.Equal(tip, repository.Git("rev-parse", "refs/heads/topic").Trim());
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenADivergedBranch_WhenGitRefusesTheFastForward_ThenTheFailureIsReported()
    {
        using var repository = TestRepositories.WithTrackingBranch(1, 1);
        repository.Git("switch", "main");
        repository.Git("fetch", "-q");
        var old = repository.Git("rev-parse", "refs/heads/topic").Trim();
        var tip = repository.Git("rev-parse", "refs/remotes/origin/topic").Trim();
        var location = await LocateAsync(repository);

        var result = await RunAsync(
            new FastForwardBranchEffect(Tab, location, "topic", "refs/remotes/origin/topic", old, tip));

        var failure = Assert.IsType<BranchOperationFailed>(result);
        Assert.StartsWith("Could not fast-forward: ", failure.Text, StringComparison.Ordinal);
        Assert.Equal(old, repository.Git("rev-parse", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenACancelledTab_WhenCreating_ThenTheResultIsCancelledAndNothingIsCreated()
    {
        using var repository = RepositoryWithTopic();
        var location = await LocateAsync(repository);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var result = await RunWithTokenAsync(
            new CreateBranchEffect(Tab, location, "never", repository.Head, "main", false, null),
            cancelled.Token);

        Assert.IsType<BranchOperationCancelled>(result);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "never").Trim());
    }

    private static TestRepository RepositoryWithTopic() =>
        TestRepository.Create()
            .Commit("one", new TestFile("a.txt", "1"))
            .Commit("two", new TestFile("a.txt", "2"))
            .Branch("topic");

    private static TestRepository Unmerged(out string tip)
    {
        var repository = RepositoryWithTopic()
            .CheckoutNewBranch("wip")
            .Commit("only here", new TestFile("only.txt", "x"))
            .Checkout("main");
        tip = repository.Git("rev-parse", "wip").Trim();

        return repository;
    }

    private static SwitchBranchEffect SwitchTo(RepositoryLocation location, SwitchTargetKind kind, string target) =>
        new(Tab, location, kind, target, null);

    private static DeleteBranchEffect Delete(
        RepositoryLocation location,
        string name,
        string tip,
        bool force,
        string? upstream = null) =>
        new(Tab, location, name, tip, upstream, force, null);

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await TestGit.Services.Discovery.DiscoverAsync(
            repository.WorkingTreePath,
            TestContext.Current.CancellationToken);

    private static Task<AppCommand> RunAsync(BranchEffect effect) =>
        RunWithTokenAsync(effect, TestContext.Current.CancellationToken);

    private static async Task<AppCommand> RunWithTokenAsync(BranchEffect effect, CancellationToken cancellationToken)
    {
        var dispatcher = new RecordingDispatcher();
        new BranchEffectHandler(TestGit.Services.Branches, dispatcher).Handle(effect, cancellationToken);

        return await dispatcher.NextAsync(TestContext.Current.CancellationToken);
    }
}
