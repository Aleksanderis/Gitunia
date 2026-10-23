using Gitunia.Git.Branches;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Branches;

public class BranchWriterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<RepositoryLocation> LocateAsync(TestRepository repository) =>
        await new RepositoryDiscovery(repository.CreateIsolatedRunner())
            .DiscoverAsync(repository.WorkingTreePath, Token);

    private static BranchWriter WriterFor(TestRepository repository) => new(repository.CreateIsolatedRunner());

    [Theory]
    [InlineData("feature/ok", true)]
    [InlineData("-leading", false)]
    [InlineData("a..b", false)]
    [InlineData("", false)]
    public async Task GivenName_WhenValidating_ThenGitRulesDecide(string name, bool expected)
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        var valid = await WriterFor(repository).IsValidNameAsync(location, name, Token);

        Assert.Equal(expected, valid);
    }

    [Fact]
    public async Task GivenCheckout_WhenCreating_ThenTheBranchIsAtTheStartPointAndCheckedOut()
    {
        using var repository = TestRepositories.Linear();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        var location = await LocateAsync(repository);

        await WriterFor(repository).CreateAsync(location, "topic", first, true, Token);

        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(first, repository.Git("rev-parse", "topic").Trim());
    }

    [Fact]
    public async Task GivenNoCheckout_WhenCreating_ThenHeadStaysWhereItWas()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);

        await WriterFor(repository).CreateAsync(location, "topic", "HEAD", false, Token);

        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal(repository.Head, repository.Git("rev-parse", "topic").Trim());
    }

    [Fact]
    public async Task GivenExistingName_WhenCreating_ThenGitRefusesAndNothingChanges()
    {
        using var repository = TestRepositories.Linear().Branch("topic");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).CreateAsync(location, "topic", "HEAD", true, Token));

        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenLocalBranch_WhenSwitching_ThenItIsCheckedOut()
    {
        using var repository = TestRepositories.Linear().Branch("topic");
        var location = await LocateAsync(repository);

        await WriterFor(repository).SwitchAsync(location, "topic", Token);

        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenRemoteBranch_WhenSwitching_ThenALocalTrackingBranchIsCreated()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.Checkout("main");
        repository.Git("branch", "-D", "topic");
        var location = await LocateAsync(repository);

        await WriterFor(repository).SwitchToRemoteAsync(location, "origin/topic", Token);

        Assert.Equal("topic", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("origin/topic", repository.Git("rev-parse", "--abbrev-ref", "topic@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenCommit_WhenSwitchingDetached_ThenHeadIsDetachedThere()
    {
        using var repository = TestRepositories.Linear();
        var first = repository.Git("rev-list", "--max-parents=0", "HEAD").Trim();
        var location = await LocateAsync(repository);

        await WriterFor(repository).SwitchDetachedAsync(location, first, Token);

        Assert.Equal(string.Empty, repository.Git("branch", "--show-current").Trim());
        Assert.Equal(first, repository.Head);
    }

    [Fact]
    public async Task GivenDirtyTreeThatConflicts_WhenSwitching_ThenGitRefusesAndTheEditSurvives()
    {
        using var repository = TestRepositories.Linear()
            .CheckoutNewBranch("topic")
            .Commit("Topic change", new TestFile("shared.txt", "topic\n"))
            .Checkout("main");
        repository.Commit("Main change", new TestFile("shared.txt", "main\n"));
        repository.WriteFile("shared.txt", "local edit\n");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).SwitchAsync(location, "topic", Token));

        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("local edit\n", File.ReadAllText(Path.Combine(repository.WorkingTreePath, "shared.txt")));
        Assert.Equal(string.Empty, repository.Git("stash", "list").Trim());
    }

    [Fact]
    public async Task GivenBranchCheckedOutInAnotherWorktree_WhenSwitching_ThenGitRefuses()
    {
        using var repository = TestRepositories.Linear().Branch("topic");
        repository.AddWorktree("wt-topic", "topic");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).SwitchAsync(location, "topic", Token));

        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenBranchWithUpstream_WhenRenaming_ThenTheNewNameKeepsTheUpstream()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        await WriterFor(repository).RenameAsync(location, "topic", "renamed", Token);

        Assert.Equal("renamed", repository.Git("branch", "--show-current").Trim());
        Assert.Equal("origin/topic", repository.Git("rev-parse", "--abbrev-ref", "renamed@{upstream}").Trim());
        Assert.Equal(string.Empty, repository.Git(["branch", "--list", "topic"], null).Trim());
    }

    [Fact]
    public async Task GivenNameInUse_WhenRenaming_ThenGitRefuses()
    {
        using var repository = TestRepositories.Linear().Branch("topic").Branch("other");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).RenameAsync(location, "topic", "other", Token));

        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenBranchWithoutUpstream_WhenSettingOne_ThenItTracksTheRemoteBranch()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var location = await LocateAsync(repository);

        await WriterFor(repository).SetUpstreamAsync(location, "lonely", "origin/main", Token);

        Assert.Equal("origin/main", repository.Git("rev-parse", "--abbrev-ref", "lonely@{upstream}").Trim());
    }

    [Fact]
    public async Task GivenUnknownRemoteBranch_WhenSettingUpstream_ThenGitRefuses()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).SetUpstreamAsync(location, "lonely", "origin/missing", Token));
    }

    [Fact]
    public async Task GivenTrackingBranch_WhenUnsettingUpstream_ThenNoUpstreamRemains()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        var location = await LocateAsync(repository);

        await WriterFor(repository).UnsetUpstreamAsync(location, "topic", Token);

        Assert.Equal(string.Empty, repository.Git("for-each-ref", "--format=%(upstream)", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenBranchWithoutUpstream_WhenUnsetting_ThenGitRefuses()
    {
        using var repository = TestRepositories.WithoutUpstreamBranch();
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).UnsetUpstreamAsync(location, "lonely", Token));
    }

    private static TestRepository TrackingBehind(int ahead, int behind)
    {
        var repository = TestRepositories.WithTrackingBranch(ahead, behind);
        repository.Git("switch", "main");
        repository.Git("fetch", "-q");
        return repository;
    }

    [Fact]
    public async Task GivenBranchBehindItsUpstream_WhenFastForwarding_ThenItMovesWithoutCheckout()
    {
        using var repository = TrackingBehind(0, 2);
        var location = await LocateAsync(repository);
        var upstreamTip = repository.Git("rev-parse", "refs/remotes/origin/topic").Trim();

        await WriterFor(repository).FastForwardAsync(location, "topic", "refs/remotes/origin/topic", Token);

        Assert.Equal(upstreamTip, repository.Git("rev-parse", "refs/heads/topic").Trim());
        Assert.Equal("main", repository.Git("branch", "--show-current").Trim());
    }

    [Fact]
    public async Task GivenDivergedBranch_WhenFastForwarding_ThenGitRefusesAndNothingMoves()
    {
        using var repository = TrackingBehind(1, 1);
        var location = await LocateAsync(repository);
        var before = repository.Git("rev-parse", "refs/heads/topic").Trim();

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).FastForwardAsync(location, "topic", "refs/remotes/origin/topic", Token));

        Assert.Equal(before, repository.Git("rev-parse", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenCurrentBranch_WhenFastForwarding_ThenGitRefuses()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 1);
        repository.Git("fetch", "-q");
        var location = await LocateAsync(repository);
        var before = repository.Git("rev-parse", "refs/heads/topic").Trim();

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).FastForwardAsync(location, "topic", "refs/remotes/origin/topic", Token));

        Assert.Equal(before, repository.Git("rev-parse", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenBranchInAnotherWorktree_WhenFastForwarding_ThenGitRefuses()
    {
        using var repository = TrackingBehind(0, 1);
        repository.AddWorktree("wt-topic", "topic");
        var location = await LocateAsync(repository);
        var before = repository.Git("rev-parse", "refs/heads/topic").Trim();

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).FastForwardAsync(location, "topic", "refs/remotes/origin/topic", Token));

        Assert.Equal(before, repository.Git("rev-parse", "refs/heads/topic").Trim());
    }

    [Fact]
    public async Task GivenMergedBranch_WhenDeleting_ThenItIsGone()
    {
        using var repository = TestRepositories.Linear().Branch("topic");
        var location = await LocateAsync(repository);
        var writer = WriterFor(repository);

        var merged = await writer.IsMergedAsync(location, "topic", null, Token);
        await writer.DeleteAsync(location, "topic", false, Token);

        Assert.True(merged);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenUnmergedBranch_WhenDeletingWithoutForce_ThenGitRefusesUntilForced()
    {
        using var repository = TestRepositories.Linear()
            .CheckoutNewBranch("topic")
            .Commit("Only here", new TestFile("only.txt", "x\n"));
        var tip = repository.Head;
        repository.Checkout("main");
        var location = await LocateAsync(repository);
        var writer = WriterFor(repository);

        var merged = await writer.IsMergedAsync(location, "topic", null, Token);
        await Assert.ThrowsAsync<GitCommandFailedException>(() => writer.DeleteAsync(location, "topic", false, Token));
        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
        await writer.DeleteAsync(location, "topic", true, Token);

        Assert.False(merged);
        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
        Assert.Equal(tip, repository.Git("rev-parse", tip).Trim());
    }

    [Fact]
    public async Task GivenBranchPushedToItsUpstream_WhenCheckingMerged_ThenItCountsAsMergedThoughNotInHead()
    {
        using var repository = TestRepositories.WithTrackingBranch(0, 0);
        repository.Checkout("main");
        var location = await LocateAsync(repository);
        var writer = WriterFor(repository);

        var withUpstream = await writer.IsMergedAsync(location, "topic", "refs/remotes/origin/topic", Token);
        var withoutUpstream = await writer.IsMergedAsync(location, "topic", null, Token);

        Assert.True(withUpstream);
        Assert.False(withoutUpstream);
    }

    [Fact]
    public async Task GivenBranchCheckedOutInAnotherWorktree_WhenDeleting_ThenGitRefusesEvenWithForce()
    {
        using var repository = TestRepositories.Linear().Branch("topic");
        repository.AddWorktree("wt-topic", "topic");
        var location = await LocateAsync(repository);

        await Assert.ThrowsAsync<GitCommandFailedException>(
            () => WriterFor(repository).DeleteAsync(location, "topic", true, Token));

        Assert.NotEqual(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }

    [Fact]
    public async Task GivenCancelledToken_WhenCreating_ThenNothingIsCreated()
    {
        using var repository = TestRepositories.Linear();
        var location = await LocateAsync(repository);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WriterFor(repository).CreateAsync(location, "topic", "HEAD", false, source.Token));

        Assert.Equal(string.Empty, repository.Git("branch", "--list", "topic").Trim());
    }
}
