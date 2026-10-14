using System.Diagnostics;
using System.Runtime.CompilerServices;
using Gitunia.Core.Graph;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories;

namespace Gitunia.Tui.Loading.Graph;

/// <summary>
/// Streams the commit log of a repository as batches of laid-out graph rows. Lanes are assigned here, off the UI
/// thread; the consumer only appends finished rows. The first batch is small so the list is usable at once.
/// </summary>
public static class GraphLoader
{
    private const int FirstBatchSize = 150;
    private const int BatchSize = 2000;
    private static readonly TimeSpan MaximumBatchAge = TimeSpan.FromMilliseconds(40);

    /// <param name="repo">The repository to read.</param>
    /// <param name="order">Commit order of the log.</param>
    /// <param name="maximumCommits">Stop after this many commits, or read everything when null.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    public static async IAsyncEnumerable<IReadOnlyList<GraphEntry>> StreamAsync(
        Repository repository,
        CommitOrder order,
        int? maximumCommits,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var builder = new GraphLayoutBuilder();
        var batch = new List<GraphEntry>(FirstBatchSize);
        var batchSize = FirstBatchSize;
        var batchAge = Stopwatch.StartNew();
        var total = 0;

        await foreach (var commit in CommitLogReader.ReadAsync(repository, order, [], cancellationToken).ConfigureAwait(false))
        {
            var row = builder.Append(commit);
            batch.Add(new GraphEntry(commit.Id, commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, row));
            total++;
            if (total == maximumCommits)
            {
                break;
            }

            if (batch.Count >= batchSize || batchAge.Elapsed > MaximumBatchAge)
            {
                yield return batch;
                batch = new List<GraphEntry>(BatchSize);
                batchSize = BatchSize;
                batchAge.Restart();
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }
}
