using Gitunia.Core.Graph;
using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App.Loading.Graph;

/// <summary>
/// Reloads a graph after a refresh without reading the whole log: reads the new log from the top and stops at the first row that
/// is the same as in the previous graph (see <see cref="GraphSplicer"/>), then reuses the rest. A cheap commit count
/// guards the reused part; when it disagrees, or no row joins, the whole log is read as before. A reload from here can
/// in theory differ from a full read when a `git replace` ref changes history below the join without changing the
/// number of commits, so the manual refresh (which does a full read) stays the way to resynchronize.
/// </summary>
public sealed class IncrementalGraphLoader
{
    private readonly ICommitLogReader _commitLog;
    private readonly GraphLoader _graphLoader;

    public IncrementalGraphLoader(ICommitLogReader commitLog, GraphLoader graphLoader)
    {
        _commitLog = commitLog;
        _graphLoader = graphLoader;
    }

    /// <param name="location">The repository to read.</param>
    /// <param name="previous">The rows currently shown (a copy that nobody changes meanwhile); empty means a full read.</param>
    /// <param name="isCapped">A full read stops at the soft row limit (the tab has not loaded its whole history); give
    /// no previous rows then.</param>
    /// <param name="cancellationToken">Cancels the read and kills git.</param>
    public async Task<GraphReload> ReloadAsync(
        RepositoryLocation location,
        IReadOnlyList<GraphEntry> previous,
        bool isCapped,
        CancellationToken cancellationToken)
    {
        if (previous.Count == 0)
        {
            return await ReadAllAsync(location, isCapped, cancellationToken).ConfigureAwait(false);
        }

        var splicer = new GraphSplicer(previous);
        using var tipRead = _graphLoader.StartTipRead(location, cancellationToken);
        GraphLayoutBuilder? builder = null;
        var rows = new List<GraphEntry>();
        var joinIndex = -1;
        await foreach (var commit in _commitLog.ReadAsync(location, CommitOrder.ByDate, [], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            builder ??= await tipRead.CreateBuilderAsync().ConfigureAwait(false);
            var entry = new GraphEntry(commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, builder.Append(commit));
            rows.Add(entry);
            joinIndex = splicer.Observe(entry);
            if (joinIndex >= 0)
            {
                break;
            }
        }

        var decorations = await tipRead.Decorations.ConfigureAwait(false);
        if (joinIndex < 0)
        {
            return new GraphReload(rows, 0);
        }

        if (decorations is null)
        {
            return await ReadAllAsync(location, false, cancellationToken).ConfigureAwait(false);
        }

        var expectedCount = rows.Count + previous.Count - joinIndex - 1;
        if (await _commitLog.CountAsync(location, cancellationToken).ConfigureAwait(false) != expectedCount)
        {
            return await ReadAllAsync(location, false, cancellationToken).ConfigureAwait(false);
        }

        var spliced = splicer.Splice(rows, joinIndex, decorations);

        // A reused tip that lost its last ref may be a commit that is gone (the count can still match when new commits arrived).
        if (spliced is null)
        {
            return await ReadAllAsync(location, false, cancellationToken).ConfigureAwait(false);
        }

        return new GraphReload(spliced, previous.Count - joinIndex - 1);
    }

    private async Task<GraphReload> ReadAllAsync(
        RepositoryLocation location,
        bool isCapped,
        CancellationToken cancellationToken)
    {
        var entries = new List<GraphEntry>();
        var cut = new GraphCut();
        var stream = _graphLoader.StreamAsync(location, CommitOrder.ByDate, null, cancellationToken);
        if (isCapped)
        {
            stream = _graphLoader.StreamCappedAsync(location, cut, cancellationToken);
        }

        await foreach (var batch in stream.ConfigureAwait(false))
        {
            entries.AddRange(batch);
        }

        return new GraphReload(entries, 0, cut.TruncatedTotal);
    }
}
