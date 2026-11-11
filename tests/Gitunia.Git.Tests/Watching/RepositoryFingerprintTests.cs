using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

public class RepositoryFingerprintTests
{
    private static readonly RepositoryDiscovery Discovery = new(IsolatedGit.CreateRunner());

    private const RepositoryChangeKinds RefsKinds = RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs;

    private const RepositoryChangeKinds StatusKinds = RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree;

    [Fact]
    public async Task GivenUnchangedRepository_WhenComputedTwice_ThenNothingDiffers()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var location = await DiscoverAsync(repository);

        var first = RepositoryFingerprint.Compute(location);
        var second = RepositoryFingerprint.Compute(location);

        Assert.Equal(RepositoryChangeKinds.None, second.ChangedKinds(first));
    }

    [Fact]
    public async Task GivenARefMove_WhenComputed_ThenOnlyHeadAndRefsDiffer()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        repository.Branch("topic");

        Assert.Equal(RefsKinds, RepositoryFingerprint.Compute(location).ChangedKinds(before));
    }

    [Fact]
    public async Task GivenAStage_WhenComputed_ThenOnlyTheStatusKindsDiffer()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        repository.WriteFile("b.txt", "2");
        repository.Git("add", "b.txt");

        Assert.Equal(StatusKinds, RepositoryFingerprint.Compute(location).ChangedKinds(before));
    }

    [Fact]
    public async Task GivenAStageThatAStatusRefreshCovered_WhenAccepted_ThenNothingDiffersAndRefsStillDo()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        repository.WriteFile("b.txt", "2");
        repository.Git("add", "b.txt");
        repository.Branch("topic");
        var current = RepositoryFingerprint.Compute(location);
        var accepted = before.Accept(current, StatusKinds);

        Assert.Equal(RefsKinds, current.ChangedKinds(accepted));
        Assert.Equal(RepositoryChangeKinds.None, current.ChangedKinds(accepted.Accept(current, RefsKinds)));
    }

    [Fact]
    public async Task GivenAnIndexWriteInAnotherWorktree_WhenComputed_ThenNothingDiffers()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var other = repository.AddWorktree("other", "other-branch", createBranch: true);
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        await File.WriteAllTextAsync(Path.Combine(other, "c.txt"), "3", TestContext.Current.CancellationToken);
        repository.GitIn(other, ["add", "c.txt"]);

        AssertUnchanged(before, RepositoryFingerprint.Compute(location));
    }

    [Fact]
    public async Task GivenAnIndexWriteInTheMainWorktree_WhenAWorktreeTabComputes_ThenNothingDiffers()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var other = repository.AddWorktree("other", "other-branch", createBranch: true);
        var location = await Discovery.DiscoverAsync(other, TestContext.Current.CancellationToken);
        var before = RepositoryFingerprint.Compute(location);

        repository.WriteFile("b.txt", "2");
        repository.Git("add", "b.txt");

        AssertUnchanged(before, RepositoryFingerprint.Compute(location));
    }

    [Fact]
    public async Task GivenAnotherWorktreeIsAdded_WhenComputed_ThenTheWorktreesKindDiffers()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        repository.AddWorktree("other", "other-branch", createBranch: true);

        var changed = RepositoryFingerprint.Compute(location).ChangedKinds(before);
        Assert.True(changed.HasFlag(RepositoryChangeKinds.Worktrees));
    }

    [Fact]
    public async Task GivenAMergeInProgress_WhenComputed_ThenTheOperationKindDiffers()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", new TestFile("a.txt", "1"));
        repository.Branch("topic");
        repository.Checkout("topic").Commit("topic", new TestFile("a.txt", "topic"));
        repository.Checkout("-").Commit("main", new TestFile("a.txt", "main"));
        var location = await DiscoverAsync(repository);
        var before = RepositoryFingerprint.Compute(location);

        repository.MergeExpectingConflict("topic");

        var changed = RepositoryFingerprint.Compute(location).ChangedKinds(before);
        Assert.True(changed.HasFlag(RepositoryChangeKinds.Operation));
    }

    private static void AssertUnchanged(RepositoryFingerprintGroups before, RepositoryFingerprintGroups after) =>
        Assert.True(after == before, $"Fingerprint groups differ: {before} -> {after}");

    private static Task<RepositoryLocation> DiscoverAsync(TestRepository repository) =>
        Discovery.DiscoverAsync(repository.WorkingTreePath, TestContext.Current.CancellationToken);
}
