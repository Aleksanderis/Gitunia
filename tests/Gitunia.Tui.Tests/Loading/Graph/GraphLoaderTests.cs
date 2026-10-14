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
    private static async Task<List<IReadOnlyList<GraphEntry>>> LoadAsync(TestRepository repository, int? maximumCommits = null)
    {
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        var batches = new List<IReadOnlyList<GraphEntry>>();
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, maximumCommits, CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }

    [Fact]
    public async Task GivenBranchyHistory_WhenStreaming_ThenRowsMatchTheBatchLayoutAndRefsAreAttached()
    {
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        repository.CheckoutNewBranch("topic").Commit("on topic", ("b.txt", "1"));
        repository.Checkout("main").Commit("on main", ("c.txt", "1"));
        repository.Merge("topic").Tag("v1");

        var entries = (await LoadAsync(repository)).SelectMany(batch => batch).ToList();

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
        using var repository = TestRepository.Create();
        repository.Commit("root", ("a.txt", "1"));
        for (var number = 1; number < 200; number++)
        {
            repository.Git("commit", "-q", "--allow-empty", "-m", "commit " + number);
        }

        var batches = await LoadAsync(repository);

        Assert.True(batches.Count >= 2);
        Assert.True(batches[0].Count <= 150);
        Assert.Equal(200, batches.Sum(batch => batch.Count));
    }

    [Fact]
    public async Task GivenMaximum_WhenStreaming_ThenStopsAfterThatManyCommits()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", ("a.txt", "1")).Commit("two", ("a.txt", "2")).Commit("three", ("a.txt", "3"));

        var entries = (await LoadAsync(repository, maximumCommits: 2)).SelectMany(batch => batch).ToList();

        Assert.Equal(["three", "two"], entries.Select(entry => entry.Subject).ToArray());
    }

    [Fact]
    public async Task GivenCancelledToken_WhenStreaming_ThenItStopsWithCancellation()
    {
        using var repository = TestRepository.Create();
        repository.Commit("one", ("a.txt", "1"));
        var repo = await Repo.OpenAsync(new GitRunner(), repository.Path, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, null, cancellation.Token))
            {
                Assert.NotEmpty(batch);
            }
        });
    }
}
