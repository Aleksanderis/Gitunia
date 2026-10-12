using Gitunia.Core.Graph;
using Gitunia.Git;

namespace Gitunia.Tui.Loading;

/// <summary>
/// Reloads a graph after a refresh without reading the whole log: reads the new log from the top and stops at the first row that
/// is the same as in the previous graph (see <see cref="GraphSplicer"/>), then reuses the rest. A cheap commit count guards the
/// reused part; when it disagrees, or no row joins, the whole log is read as before. A reload from here can in theory differ from a
/// full read when history below the join was rewritten without changing the number of commits, so the manual refresh (which does a
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
        if (await CountCommitsAsync(repo, cancellationToken).ConfigureAwait(false) != expectedCount)
        {
            return await ReadAllAsync(repo, cancellationToken).ConfigureAwait(false);
        }

        var decorations = await ReadTipDecorationsAsync(repo, cancellationToken).ConfigureAwait(false);
        return new GraphReload(splicer.Splice(rows, joinIndex, decorations), previous.Count - joinIndex - 1);
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

    private static async Task<int> CountCommitsAsync(Repo repo, CancellationToken cancellationToken)
    {
        var result = await repo.RunAsync(["rev-list", "--count", "--exclude=refs/stash", "--all", "--"], new GitRunOptions(ReadOnly: true), cancellationToken).ConfigureAwait(false);
        return int.TryParse(result.StandardOutputText.Trim(), out var count) ? count : -1;
    }

    // Only the commits that carry a ref (a few hundred at most): the same decoration text the full log prints for them.
    private static async Task<Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>> ReadTipDecorationsAsync(Repo repo, CancellationToken cancellationToken)
    {
        var decorations = new Dictionary<ObjectId, IReadOnlyList<CommitDecoration>>();
        await foreach (var commit in CommitLogReader.ReadAsync(repo, CommitOrder.Topological, ["--no-walk", "--exclude=refs/stash", "--all"], cancellationToken).ConfigureAwait(false))
        {
            if (commit.Decorations.Count > 0)
            {
                decorations[commit.Id] = commit.Decorations;
            }
        }

        return decorations;
    }
}
