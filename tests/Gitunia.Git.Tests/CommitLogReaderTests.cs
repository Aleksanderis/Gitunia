using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests;

public class CommitLogReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenBranchyRepository_WhenReadingLog_ThenEveryCommitAppearsChildrenBeforeParents()
    {
        using var repository = TestRepositories.Branchy();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var commits = await ReadAllAsync(repo);

        Assert.Equal(repository.Git("rev-list", "--all", "--count").Trim(), commits.Count.ToString());
        var positions = commits.Select((commit, index) => (commit.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        foreach (var commit in commits)
        {
            Assert.All(commit.Parents, parent => Assert.True(positions[parent] > positions[commit.Id]));
        }
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReadingLog_ThenHasTwoParentsAndDecorations()
    {
        using var repository = TestRepositories.Branchy();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var commits = await ReadAllAsync(repo);

        var head = commits.Single(commit => commit.Id.ToString() == repository.Head);
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
        using var repository = TestRepository.Create()
            .Commit("ünï 日本 \"quoted\" subject; with | pipes", ("a.txt", "a\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var commits = await ReadAllAsync(repo);

        Assert.Equal("ünï 日本 \"quoted\" subject; with | pipes", Assert.Single(commits).Subject);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHeadLog_ThenHeadDecorationAppears()
    {
        using var repository = TestRepositories.Linear(2);
        repository.Git("checkout", "-q", "--detach", "HEAD");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var commits = await ReadAllAsync(repo, CommitOrder.ByDate, "HEAD");

        Assert.Contains(commits[0].Decorations, decoration => decoration.Kind == RefKind.Head);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingLog_ThenEmpty()
    {
        using var repository = TestRepository.Create();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Empty(await ReadAllAsync(repo));
    }

    [Fact]
    public async Task GivenSingleBranchRevision_WhenReadingLog_ThenOnlyThatHistory()
    {
        using var repository = TestRepositories.Branchy();
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        var commits = await ReadAllAsync(repo, CommitOrder.Topological, "feature");

        Assert.Equal(3, commits.Count);
    }

    [Fact]
    public async Task GivenBigRepositoryFromEnvironment_WhenStreamingLog_ThenAllCommitsParseWithBoundedBuffer()
    {
        var bigRepository = Environment.GetEnvironmentVariable("GITUNIA_BIG_REPO");
        Assert.SkipUnless(bigRepository is not null && Directory.Exists(bigRepository), "set GITUNIA_BIG_REPO to a repo from scripts/gen-big-repo.sh to run this");
        var repo = await Repo.OpenAsync(Runner, bigRepository!, Token);
        var expected = long.Parse((await repo.RunAsync(["rev-list", "--all", "--count"], cancellationToken: Token)).StandardOutputText.Trim());

        var count = 0L;
        await foreach (var commit in CommitLogReader.ReadAsync(repo, CommitOrder.Topological, [], Token))
        {
            count += commit.Id.IsValid ? 1 : 0;
        }

        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task GivenUnknownRevision_WhenReadingLog_ThenGitErrorPropagates()
    {
        using var repository = TestRepositories.Linear(1);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReadAllAsync(repo, CommitOrder.Topological, "no-such-branch"));
    }

    [Fact]
    public async Task GivenRevisionNamedLikeAFile_WhenReadingLog_ThenItIsStillTreatedAsRevision()
    {
        using var repository = TestRepository.Create().Commit("One", ("main", "file named like the branch\n"));
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Single(await ReadAllAsync(repo, CommitOrder.Topological, "main"));
    }

    [Fact]
    public async Task GivenStash_WhenReadingDefaultLog_ThenStashCommitsAreExcluded()
    {
        using var repository = TestRepositories.Linear(1);
        repository.WriteFile("file.txt", "changed\n");
        repository.Git("stash", "push", "-q");
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);

        Assert.Single(await ReadAllAsync(repo));
    }

    [Fact]
    public async Task GivenCancellation_WhenReadingLog_ThenOperationCancelled()
    {
        using var repository = TestRepositories.Linear(3);
        var repo = await Repo.OpenAsync(Runner, repository.Path, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var commit in CommitLogReader.ReadAsync(repo, CommitOrder.Topological, [], source.Token))
            {
                Assert.NotNull(commit);
            }
        });
    }

    private static async Task<List<CommitInfo>> ReadAllAsync(Repo repo, CommitOrder order = CommitOrder.Topological, params string[] revisions)
    {
        var commits = new List<CommitInfo>();
        await foreach (var commit in CommitLogReader.ReadAsync(repo, order, revisions, Token))
        {
            commits.Add(commit);
        }

        return commits;
    }
}
