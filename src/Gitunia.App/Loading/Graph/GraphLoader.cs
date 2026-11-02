using System.Diagnostics;
using System.Runtime.CompilerServices;
using Gitunia.Core.Graph;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Loading.Graph;

/// <summary>
/// Streams the commit log of a repository as batches of laid-out graph rows. Lanes are assigned here, off the UI
/// thread; the consumer only appends finished rows. The first batch is small so the list is usable at once.
/// </summary>
public sealed class GraphLoader
{
    /// <summary>Rows a tab loads eagerly; a longer log is cut here and the rest loads on demand (#515).</summary>
    public const int DefaultSoftRowLimit = 100_000;

    private const int FirstBatchSize = 150;
    private const int BatchSize = 2000;
    private static readonly TimeSpan MaximumBatchAge = TimeSpan.FromMilliseconds(40);

    private readonly ICommitLogReader _commitLog;
    private readonly ILaneColors _laneColors;

    /// <param name="commitLog">Where the log comes from.</param>
    /// <param name="softRowLimit">Rows a tab loads eagerly.</param>
    /// <param name="laneColors">The distinct colours of the palette, read per load; all of them when null.</param>
    public GraphLoader(
        ICommitLogReader commitLog,
        int softRowLimit = DefaultSoftRowLimit,
        ILaneColors? laneColors = null)
    {
        _commitLog = commitLog;
        _laneColors = laneColors ?? AllLaneColors.Instance;
        SoftRowLimit = softRowLimit;
    }

    /// <summary>Rows a tab loads eagerly (see <see cref="StreamCappedAsync"/>).</summary>
    public int SoftRowLimit { get; }

    /// <summary>
    /// Like <see cref="StreamAsync"/> in topological order, but stops after <see cref="SoftRowLimit"/> rows when the
    /// log is longer and says so in <paramref name="cut"/> (with the commit count of the whole log).
    /// </summary>
    /// <param name="location">The repository to read.</param>
    /// <param name="cut">Receives the total when the log was cut.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    public async IAsyncEnumerable<IReadOnlyList<GraphEntry>> StreamCappedAsync(
        RepositoryLocation location,
        GraphCut cut,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cut.TruncatedTotal = null;
        var limit = SoftRowLimit;

        // One row more than the limit tells whether there is more.
        var kept = 0;
        var exceeded = false;
        var stream = StreamAsync(location, CommitOrder.ByDate, limit + 1, cancellationToken);
        await foreach (var batch in stream.ConfigureAwait(false))
        {
            var room = limit - kept;
            var isOver = batch.Count > room;
            exceeded |= isOver;
            var part = isOver ? batch.Take(room).ToList() : batch;
            kept += part.Count;
            if (part.Count > 0)
            {
                yield return part;
            }
        }

        if (exceeded)
        {
            var total = await _commitLog.CountAsync(location, cancellationToken).ConfigureAwait(false);
            cut.TruncatedTotal = Math.Max(total, limit + 1);
        }
    }

    /// <param name="location">The repository to read.</param>
    /// <param name="order">Commit order of the log.</param>
    /// <param name="maximumCommits">Stop after this many commits, or read everything when null.</param>
    /// <param name="cancellationToken">Cancels the stream and kills git.</param>
    public async IAsyncEnumerable<IReadOnlyList<GraphEntry>> StreamAsync(
        RepositoryLocation location,
        CommitOrder order,
        int? maximumCommits,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Started before the log so the extra git call overlaps with git producing the first rows.
        using var tipRead = StartTipRead(location, cancellationToken);
        GraphLayoutBuilder? builder = null;
        var batch = new List<GraphEntry>(FirstBatchSize);
        var batchSize = FirstBatchSize;
        var batchAge = Stopwatch.StartNew();
        var total = 0;

        await foreach (var commit in _commitLog.ReadAsync(location, order, [], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            builder ??= await tipRead.CreateBuilderAsync().ConfigureAwait(false);
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

        await tipRead.Decorations.ConfigureAwait(false);
    }

    /// <summary>Starts reading the branch tips of a repository for a layout (dispose it after the log read).</summary>
    /// <param name="location">The repository to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public BranchTipRead StartTipRead(RepositoryLocation location, CancellationToken cancellationToken) =>
        new(_commitLog, location, _laneColors.Count, cancellationToken);
}
