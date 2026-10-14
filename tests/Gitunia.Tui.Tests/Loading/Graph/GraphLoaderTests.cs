using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.Loading.Graph;
using Xunit;

namespace Gitunia.Tui.Tests.Loading.Graph;

public class GraphLoaderTests
{
    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(TestRepository testRepository, int? maximumCommits = null)
    {
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, CancellationToken.None);
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in GraphLoader.StreamAsync(repository, CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }

    [Fact]
    public async Task GivenBranchyHistory_WhenStreaming_ThenRowsMatchTheBatchLayoutAndRefsAreAttached()
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
        using var testRepository = TestRepository.Create();
        testRepository.Commit("root", new TestFile("a.txt", "1"));
        for (var number = 1; number < 200; number++)
        {
            testRepository.Git("commit", "-q", "--allow-empty", "-m", $"commit {number}");
        }

        var batches = await LoadAsync(testRepository);

        Assert.True(batches.Count >= 2);
        Assert.True(batches[0].Count <= 150);
        Assert.Equal(200, batches.Sum(batch => batch.Count));
    }

    [Fact]
    public async Task GivenMaximum_WhenStreaming_ThenStopsAfterThatManyCommits()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Commit("one", new TestFile("a.txt", "1")).Commit("two", new TestFile("a.txt", "2")).Commit("three", new TestFile("a.txt", "3"));

        var entries = (await LoadAsync(testRepository, maximumCommits: 2)).SelectMany(batch => batch).ToList();

        Assert.Equal(["three", "two"], entries.Select(entry => entry.Subject).ToArray());
    }

    [Fact]
    public async Task GivenCancelledToken_WhenStreaming_ThenItStopsWithCancellation()
    {
        using var testRepository = TestRepository.Create();
        testRepository.Commit("one", new TestFile("a.txt", "1"));
        var repository = await Repository.OpenAsync(new GitRunner(), testRepository.WorkingTreePath, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in GraphLoader.StreamAsync(repository, CommitOrder.Topological, null, cancellation.Token))
            {
                Assert.NotEmpty(batch);
            }
        });
    }
}
