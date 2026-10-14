using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitLogReaderTests
{
    private static readonly GitRunner Runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenBranchyRepository_WhenReadingLog_ThenEveryCommitAppearsChildrenBeforeParents()
    {
        using var testRepository = TestRepositories.Branchy();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository);

        Assert.Equal(testRepository.Git("rev-list", "--all", "--count").Trim(), commits.Count.ToString());
        var positions = commits.Select((commit, index) => (commit.Id, index)).ToDictionary(pair => pair.Id, pair => pair.index);
        foreach (var commit in commits)
        {
            Assert.All(commit.Parents, parent => Assert.True(positions[parent] > positions[commit.Id]));
        }
    }

    [Fact]
    public async Task GivenCommitWithWestOffset_WhenReadingLog_ThenAuthorOffsetAndLocalDateAreKept()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.Git("commit", "--allow-empty", "-m", "late", "--date=2026-10-13T23:30:00-0500");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository);

        var late = commits.Single(commit => commit.Subject == "late");
        Assert.Equal(TimeSpan.FromHours(-5), late.AuthorTime.Offset);
        Assert.Equal(new DateTime(2026, 10, 13, 23, 30, 0), late.AuthorTime.DateTime);
    }

    [Fact]
    public async Task GivenMergeCommit_WhenReadingLog_ThenHasTwoParentsAndDecorations()
    {
        using var testRepository = TestRepositories.Branchy();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository);

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
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository);

        Assert.Equal("ünï 日本 \"quoted\" subject; with | pipes", Assert.Single(commits).Subject);
    }

    [Fact]
    public async Task GivenDetachedHead_WhenReadingHeadLog_ThenHeadDecorationAppears()
    {
        using var testRepository = TestRepositories.Linear(2);
        testRepository.Git("checkout", "-q", "--detach", "HEAD");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository, CommitOrder.ByDate, "HEAD");

        Assert.Contains(commits[0].Decorations, decoration => decoration.Kind == RefKind.Head);
    }

    [Fact]
    public async Task GivenUnbornBranch_WhenReadingLog_ThenEmpty()
    {
        using var testRepository = TestRepository.Create();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Empty(await ReadAllAsync(repository));
    }

    [Fact]
    public async Task GivenSingleBranchRevision_WhenReadingLog_ThenOnlyThatHistory()
    {
        using var testRepository = TestRepositories.Branchy();
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        var commits = await ReadAllAsync(repository, CommitOrder.Topological, "feature");

        Assert.Equal(3, commits.Count);
    }

    [Fact]
    public async Task GivenBigRepositoryFromEnvironment_WhenStreamingLog_ThenAllCommitsParseWithBoundedBuffer()
    {
        var bigRepository = Environment.GetEnvironmentVariable("GITUNIA_BIG_REPO");
        Assert.SkipUnless(bigRepository is not null && Directory.Exists(bigRepository), "set GITUNIA_BIG_REPO to a repo from scripts/gen-big-repo.sh to run this");
        var repository = await Repository.OpenAsync(Runner, bigRepository!, Token);
        var expected = long.Parse((await repository.RunAsync(["rev-list", "--all", "--count"], cancellationToken: Token)).StandardOutputText.Trim());

        var count = 0L;
        await foreach (var commit in CommitLogReader.ReadAsync(repository, CommitOrder.Topological, [], cancellationToken: Token))
        {
            count += commit.Id.IsValid ? 1 : 0;
        }

        Assert.Equal(expected, count);
    }

    [Fact]
    public async Task GivenUnknownRevision_WhenReadingLog_ThenGitErrorPropagates()
    {
        using var testRepository = TestRepositories.Linear(1);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        await Assert.ThrowsAsync<GitCommandFailedException>(() => ReadAllAsync(repository, CommitOrder.Topological, "no-such-branch"));
    }

    [Fact]
    public async Task GivenRevisionNamedLikeAFile_WhenReadingLog_ThenItIsStillTreatedAsRevision()
    {
        using var testRepository = TestRepository.Create().Commit("One", new TestFile("main", "file named like the branch\n"));
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Single(await ReadAllAsync(repository, CommitOrder.Topological, "main"));
    }

    [Fact]
    public async Task GivenStash_WhenReadingDefaultLog_ThenStashCommitsAreExcluded()
    {
        using var testRepository = TestRepositories.Linear(1);
        testRepository.WriteFile("file.txt", "changed\n");
        testRepository.Git("stash", "push", "-q");
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);

        Assert.Single(await ReadAllAsync(repository));
    }

    [Fact]
    public async Task GivenCancellation_WhenReadingLog_ThenOperationCancelled()
    {
        using var testRepository = TestRepositories.Linear(3);
        var repository = await Repository.OpenAsync(Runner, testRepository.Path, Token);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var commit in CommitLogReader.ReadAsync(repository, CommitOrder.Topological, [], cancellationToken: source.Token))
            {
                Assert.NotNull(commit);
            }
        });
    }

    private static async Task<List<CommitInfo>> ReadAllAsync(Repository repository, CommitOrder order = CommitOrder.Topological, params string[] revisions)
    {
        var commits = new List<CommitInfo>();
        await foreach (var commit in CommitLogReader.ReadAsync(repository, order, revisions, cancellationToken: Token))
        {
            commits.Add(commit);
        }

        return commits;
    }
}
