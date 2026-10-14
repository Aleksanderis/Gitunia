using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Graph;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

// The merged-history case keeps a real repository: the lanes and the decorations come from real topology and real refs. The batching,
// limit, cancellation and failure cases only need a stream of commits and run on a fake commit log.
public class GraphLoaderTests
{
    [Fact]
    public async Task GivenMergedFeatureBranchHistory_WhenStreaming_ThenRowsMatchTheBatchLayoutAndRefsAreAttached()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Commit("root", new TestFile("a.txt", "1"));
        testRepository.CheckoutNewBranch("topic").Commit("on topic", new TestFile("b.txt", "1"));
        testRepository.Checkout("main").Commit("on main", new TestFile("c.txt", "1"));
        testRepository.Merge("topic").Tag("v1");

        var entries = (await LoadAsync(testRepository)).SelectMany(batch => batch).ToList();

        Assert.Equal("Merge topic", entries[0].Subject);
        Assert.Equal("root", entries[3].Subject);
        Assert.Equal(["on main", "on topic"], entries.Skip(1).Take(2).Select(entry => entry.Subject).Order().ToArray());
        Assert.Equal(entries.Select(entry => entry.Id.ToString()).ToArray(), entries.Select(entry => entry.Row.Id.ToString()).ToArray());
        Assert.Equal(2, entries[0].Row.Edges.Count(edge => edge.Kind == GraphEdgeKind.Outgoing));
        Assert.Contains(entries[0].Decorations, decoration => decoration.Name == "main" && decoration.IsCurrent);
        Assert.Contains(entries[0].Decorations, decoration => decoration.Name == "v1");
        Assert.Contains(entries.Single(entry => entry.Subject == "on topic").Decorations, decoration => decoration.Name == "topic");
    }

    [Fact]
    public async Task GivenMoreCommitsThanTheFirstBatch_WhenStreaming_ThenFirstBatchIsSmallAndNothingIsLost()
    {
        var batches = await LoadAsync(FakeCommitLogReader.Chain(200));

        Assert.True(batches.Count >= 2);
        Assert.True(batches[0].Count <= 150);
        Assert.Equal(200, batches.Sum(batch => batch.Count));
    }

    [Fact]
    public async Task GivenMaximum_WhenStreaming_ThenStopsAfterThatManyCommits()
    {
        var entries = (await LoadAsync(FakeCommitLogReader.Chain(3), maximumCommits: 2)).SelectMany(batch => batch).ToList();

        Assert.Equal(["commit 3", "commit 2"], entries.Select(entry => entry.Subject).ToArray());
    }

    [Fact]
    public async Task GivenCancelledToken_WhenStreaming_ThenItStopsWithCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in new GraphLoader(FakeCommitLogReader.Chain(1)).StreamAsync(FakeStatusReader.LocationOf("graph"), CommitOrder.Topological, null, cancellation.Token))
            {
                Assert.NotEmpty(batch);
            }
        });
    }

    [Fact]
    public async Task GivenReadFailure_WhenStreaming_ThenTheFailureReachesTheCaller()
    {
        var commitLog = FakeCommitLogReader.Chain(1);
        commitLog.Failure = new GitException("boom");

        await Assert.ThrowsAsync<GitException>(() => LoadAsync(commitLog));
    }

    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(TestRepository testRepository, int? maximumCommits = null)
    {
        var location = await TestGit.Services.Discovery.DiscoverAsync(testRepository.WorkingTreePath, CancellationToken.None);
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in TestGit.Services.GraphLoader.StreamAsync(location, CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }

    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(FakeCommitLogReader commitLog, int? maximumCommits = null)
    {
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in new GraphLoader(commitLog).StreamAsync(FakeStatusReader.LocationOf("graph"), CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }
}
