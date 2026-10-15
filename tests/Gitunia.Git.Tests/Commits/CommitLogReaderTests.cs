using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitLogReaderTests
{
    private static readonly GitRunner Runner = new();
    private static readonly RepositoryDiscovery Discovery = new(Runner);
    private static readonly CommitLogReader CommitLog = new(Runner);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenMergedFeatureBranchRepository_WhenReadingLog_ThenEveryCommitAppearsChildrenBeforeParents()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location);

        Assert.Equal(testRepository.Git("rev-list", "--all", "--count").Trim(), commits.Count.ToString());
        var positions = new Dictionary<ObjectId, int>();
        for (var index = 0; index < commits.Count; index++)
        {
            positions[commits[index].Id] = index;
        }

        foreach (var commit in commits)
        {
            Assert.All(commit.Parents, parent => Assert.True(positions[parent] > positions[commit.Id]));
        }
    }

    [Fact]
    public async Task GivenUnbornHead_WhenReadingCurrentBranchTip_ThenNull()
    {
        using var testRepository = TestRepository.Create();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Null(await CommitLog.ReadCurrentBranchTipAsync(location, Token));
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingCurrentBranchTip_ThenNull()
    {
        using var testRepository = TestRepositories.Linear(3);
        testRepository.Git("checkout", "-q", "--detach", "HEAD~1");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Null(await CommitLog.ReadCurrentBranchTipAsync(location, Token));
    }

    [Fact]
    public async Task GivenAnotherBranchCheckedOutInALinkedWorktree_WhenReadingCurrentBranchTip_ThenItIsTheTipOfTheBranchOfThisWorktree()
    {
        using var testRepository = TestRepositories.Linear(3);
        testRepository.AddWorktree("side", "side", createBranch: true);
        testRepository.Git("-C", testRepository.AddWorktree("side-2", "side-2", createBranch: true), "commit", "-q", "--allow-empty", "-m", "elsewhere");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var tip = await CommitLog.ReadCurrentBranchTipAsync(location, Token);

        Assert.Equal(testRepository.Head, tip?.ToString());
    }

    [Fact]
    public async Task GivenCommitWithWestOffset_WhenReadingLog_ThenAuthorOffsetAndLocalDateAreKept()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Git("commit", "--allow-empty", "-m", "late", "--date=2026-10-13T23:30:00-0500");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location);

        var late = commits.Single(commit => commit.Subject == "late");
        Assert.Equal(TimeSpan.FromHours(-5), late.AuthorTime.Offset);
        Assert.Equal(new DateTime(2026, 10, 13, 23, 30, 0), late.AuthorTime.DateTime);
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReadingLog_ThenHasTwoParentsAndDecorations()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location);

        var head = commits.Single(commit => commit.Id.ToString() == testRepository.Head);
        Assert.Equal(2, head.Parents.Count);
        Assert.Contains(new CommitDecoration("refs/heads/main", "main", RefKind.LocalBranch, true), head.Decorations);
        Assert.Contains(head.Decorations, decoration => decoration.Kind == RefKind.Tag && decoration.Name == "v1");
        Assert.Equal("Merge feature", head.Subject);
        Assert.Equal("Test", head.AuthorName);
        Assert.Equal("test@example.com", head.AuthorEmail);
    }

    [Fact]
    public async Task GivenOddSubjects_WhenReadingLog_ThenTextSurvives()
    {
        using var testRepository = TestRepository.Create()
            .Commit("ünï 日本 \"quoted\" subject; with | pipes", new TestFile("a.txt", "a\n"));
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location);

        Assert.Equal("ünï 日本 \"quoted\" subject; with | pipes", Assert.Single(commits).Subject);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHeadLog_ThenHeadDecorationAppears()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Git("checkout", "-q", "--detach", "HEAD");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location, CommitOrder.ByDate, "HEAD");

        Assert.Contains(commits[0].Decorations, decoration => decoration.Kind == RefKind.Head);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingLog_ThenEmpty()
    {
        using var testRepository = TestRepository.Create();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Empty(await ReadAllAsync(location));
    }

    [Fact]
    public async Task GivenSingleBranchRevision_WhenReadingLog_ThenOnlyThatHistory()
    {
        using var testRepository = TestRepositories.WithMergedFeatureBranch();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        var commits = await ReadAllAsync(location, CommitOrder.Topological, "feature");

        Assert.Equal(3, commits.Count);
    }

    [Fact]
    public async Task GivenBigRepositoryFromEnvironment_WhenStreamingLog_ThenAllCommitsParseWithBoundedBuffer()
    {
        var bigRepository = Environment.GetEnvironmentVariable("GITUNIA_BIG_REPO");
        Assert.SkipUnless(bigRepository is not null && Directory.Exists(bigRepository), "set GITUNIA_BIG_REPO to a repo from scripts/gen-big-repo.sh to run this");
        var location = await Discovery.DiscoverAsync(bigRepository, Token);
        var expected = long.Parse((await Runner.RunAsync(location.CommandPath, ["rev-list", "--all", "--count"], cancellationToken: Token)).StandardOutputText.Trim());

        var count = 0L;
        await foreach (var commit in CommitLog.ReadAsync(location, CommitOrder.Topological, [], cancellationToken: Token))
        {
            count += commit.Id.IsValid ? 1 : 0;
        }

        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task GivenUnknownRevision_WhenReadingLog_ThenGitErrorPropagates()
    {
        using var testRepository = TestRepositories.Linear(1);
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReadAllAsync(location, CommitOrder.Topological, "no-such-branch"));
    }

    [Fact]
    public async Task GivenRevisionNamedLikeAFile_WhenReadingLog_ThenItIsStillTreatedAsRevision()
    {
        using var testRepository = TestRepository.Create().Commit("One", new TestFile("main", "file named like the branch\n"));
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Single(await ReadAllAsync(location, CommitOrder.Topological, "main"));
    }

    [Fact]
    public async Task GivenStash_WhenReadingDefaultLog_ThenStashCommitsAreExcluded()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.WriteFile("file.txt", "changed\n");
        testRepository.Git("stash", "push", "-q");
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);

        Assert.Single(await ReadAllAsync(location));
    }

    [Fact]
    public async Task GivenCancellation_WhenReadingLog_ThenOperationCancelled()
    {
        using var testRepository = TestRepositories.Linear();
        var location = await Discovery.DiscoverAsync(testRepository.WorkingTreePath, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var commit in CommitLog.ReadAsync(location, CommitOrder.Topological, [], cancellationToken: source.Token))
            {
                Assert.NotNull(commit);
            }
        });
    }

    private static async Task<List<CommitInfo>> ReadAllAsync(RepositoryLocation location, CommitOrder order = CommitOrder.Topological, params string[] revisions)
    {
        var commits = new List<CommitInfo>();
        await foreach (var commit in CommitLog.ReadAsync(location, order, revisions, cancellationToken: Token))
        {
            commits.Add(commit);
        }

        return commits;
    }
}
