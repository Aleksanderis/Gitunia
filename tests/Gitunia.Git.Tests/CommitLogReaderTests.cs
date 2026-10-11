using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class CommitLogReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<CommitInfo>> ReadAllAsync(Repo repo, CommitOrder order = CommitOrder.Topological, params string[] revisions)
    {
        var commits = new List<CommitInfo>();
        await foreach (CommitInfo commit in CommitLogReader.ReadAsync(repo, order, revisions, Token))
        {
            commits.Add(commit);
        }

        return commits;
    }

    [Fact]
    public async Task GivenBranchyRepository_WhenReadingLog_ThenEveryCommitAppearsChildrenBeforeParents()
    {
        using TestRepository repository = TestRepositories.Branchy();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        List<CommitInfo> commits = await ReadAllAsync(repo);

        Assert.Equal(repository.Git("rev-list", "--all", "--count").Trim(), commits.Count.ToString());
        var positions = commits.Select((commit, index) => (commit.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        foreach (CommitInfo commit in commits)
        {
            Assert.All(commit.Parents, parent => Assert.True(positions[parent] > positions[commit.Id]));
        }
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReadingLog_ThenHasTwoParentsAndDecorations()
    {
        using TestRepository repository = TestRepositories.Branchy();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        List<CommitInfo> commits = await ReadAllAsync(repo);

        CommitInfo head = commits.Single(commit => commit.Id.ToString() == repository.Head);
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
        using TestRepository repository = TestRepository.Create()
            .Commit("ünï 日本 \"quoted\" subject; with | pipes", ("a.txt", "a\n"));
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        List<CommitInfo> commits = await ReadAllAsync(repo);

        Assert.Equal("ünï 日本 \"quoted\" subject; with | pipes", Assert.Single(commits).Subject);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHeadLog_ThenHeadDecorationAppears()
    {
        using TestRepository repository = TestRepositories.Linear(2);
        repository.Git("checkout", "-q", "--detach", "HEAD");
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        List<CommitInfo> commits = await ReadAllAsync(repo, CommitOrder.ByDate, "HEAD");

        Assert.Contains(commits[0].Decorations, decoration => decoration.Kind == RefKind.Head);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingLog_ThenEmpty()
    {
        using TestRepository repository = TestRepository.Create();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Empty(await ReadAllAsync(repo));
    }

    [Fact]
    public async Task GivenSingleBranchRevision_WhenReadingLog_ThenOnlyThatHistory()
    {
        using TestRepository repository = TestRepositories.Branchy();
        Repo repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        List<CommitInfo> commits = await ReadAllAsync(repo, CommitOrder.Topological, "feature");

        Assert.Equal(3, commits.Count);
    }

    [Fact]
    public async Task GivenBigRepositoryFromEnvironment_WhenStreamingLog_ThenAllCommitsParseWithBoundedBuffer()
    {
        string? bigRepository = Environment.GetEnvironmentVariable("GITUNIA_BIG_REPO");
        Assert.SkipUnless(bigRepository is not null && Directory.Exists(bigRepository), "set GITUNIA_BIG_REPO to a repo from scripts/gen-big-repo.sh to run this");
        Repo repo = await Repo.OpenAsync(Runner, bigRepository!, Token);
        long expected = long.Parse((await repo.RunAsync(["rev-list", "--all", "--count"], cancellationToken: Token)).StandardOutputText.Trim());
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        long count = 0;
        await foreach (CommitInfo unused in CommitLogReader.ReadAsync(repo, CommitOrder.Topological, [], Token))
        {
            count += unused.Id.IsValid ? 1 : 0;
        }

        Assert.Equal(expected, count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"took {stopwatch.Elapsed}");
    }
}
