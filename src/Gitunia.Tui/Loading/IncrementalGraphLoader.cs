using Gitunia.Core.Graph;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// Reloads a graph after a refresh without reading the whole log: reads the new log from the top and stops at the first row that
/// is the same as in the previous graph (see <see cref="GraphSplicer"/>), then reuses the rest. A cheap commit count guards the
/// reused part; when it disagrees, or no row joins, the whole log is read as before. A reload from here can in theory differ from a
/// full read when a `git replace` ref changes history below the join without changing the number of commits, so the manual refresh (which does a
/// full read) stays the way to resynchronize.
/// </summary>
public static class IncrementalGraphLoader
{
    /// <param name="repo">The repository to read.</param>
    /// <param name="previous">The rows currently shown (a copy that nobody changes meanwhile); empty means a full read.</param>
    /// <param name="cancellationToken">Cancels the read and kills git.</param>
    public static async Task<GraphReload> ReloadAsync(Repo repo, IReadOnlyList<GraphEntry> previous, CancellationToken cancellationToken)
    {
        if (previous.Count == 0)
        {
            return await ReadAllAsync(repo, cancellationToken).ConfigureAwait(false);
        }

        var splicer = new GraphSplicer(previous);
        var builder = new GraphLayoutBuilder();
        var rows = new List<GraphEntry>();
        var joinIndex = -1;
        await foreach (var commit in CommitLogReader.ReadAsync(repo, CommitOrder.Topological, [], cancellationToken).ConfigureAwait(false))
        {
            var entry = new GraphEntry(commit.Id, commit.Subject, commit.AuthorName, commit.AuthorTime, commit.Decorations, builder.Append(commit));
            rows.Add(entry);
            joinIndex = splicer.Observe(entry);
            if (joinIndex >= 0)
            {
                break;
            }
        }

        if (joinIndex < 0)
        {
            return new GraphReload(rows, 0);
        }

        var expectedCount = rows.Count + previous.Count - joinIndex - 1;
        if (await CommitLogReader.CountAsync(repo, cancellationToken).ConfigureAwait(false) != expectedCount)
        {
            return await ReadAllAsync(repo, cancellationToken).ConfigureAwait(false);
        }

        var decorations = await CommitLogReader.ReadTipDecorationsAsync(repo, cancellationToken).ConfigureAwait(false);
        var spliced = splicer.Splice(rows, joinIndex, decorations);

        // A reused tip that lost its last ref may be a commit that is gone (the count can still match when new commits arrived).
        return spliced is null
            ? await ReadAllAsync(repo, cancellationToken).ConfigureAwait(false)
            : new GraphReload(spliced, previous.Count - joinIndex - 1);
    }

    private static async Task<GraphReload> ReadAllAsync(Repo repo, CancellationToken cancellationToken)
    {
        var entries = new List<GraphEntry>();
        await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, null, cancellationToken).ConfigureAwait(false))
        {
            entries.AddRange(batch);
        }

        return new GraphReload(entries, 0);
    }
}
