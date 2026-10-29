using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Reuses the rows of a previous graph when a refreshed log only changed near the top. Feed it the freshly laid-out rows
/// one by one with <see cref="Observe"/>; the first row that is also in the previous graph with the same lane layout and the same
/// open lanes below it (see <see cref="GraphRow.LaneFingerprint"/>) is the join: everything below it in the previous graph
/// is what the new log would produce, so the rest of the log need not be read. Only the part above the join is new.
/// </summary>
public sealed class GraphSplicer(IReadOnlyList<GraphEntry> previous)
{
    private Dictionary<ObjectId, int>? _indexById;
    private int _highestObservedIndex = -1;

    /// <summary>Number of rows of the previous graph.</summary>
    public int PreviousCount => previous.Count;

    /// <summary>
    /// Checks the next new row. Returns the index of the matching previous row when this row is the join, otherwise -1.
    /// A row also fails to join when an earlier new row sat lower in the previous graph (the order changed).
    /// </summary>
    public int Observe(GraphEntry entry)
    {
        var index = IndexById();
        if (!index.TryGetValue(entry.Id, out var previousIndex))
        {
            return -1;
        }

        var isAfterEarlierRows = previousIndex > _highestObservedIndex;
        _highestObservedIndex = Math.Max(_highestObservedIndex, previousIndex);
        return isAfterEarlierRows && SameLayout(previous[previousIndex].Row, entry.Row) ? previousIndex : -1;
    }

    /// <summary>
    /// Builds the new graph: <paramref name="newRows"/> (the rows read, the last one being the join) followed by the previous rows
    /// below the join. Rows below the join get their refs from <paramref name="decorationsByCommit"/> (every commit that carries a ref
    /// now), since refs may have moved without the commits changing.
    /// Returns null when the branch of a reused commit changed (its lane colour was chosen by the name) or a reused tip
    /// (a row without incoming edge) lost all its refs (it may be a commit that no longer exists): the caller must read
    /// the whole log then.
    /// </summary>
    public List<GraphEntry>? Splice(IReadOnlyList<GraphEntry> newRows, int joinIndex, IReadOnlyDictionary<ObjectId, IReadOnlyList<CommitDecoration>> decorationsByCommit)
    {
        var result = new List<GraphEntry>(newRows.Count + previous.Count - joinIndex - 1);
        result.AddRange(newRows);
        for (var index = joinIndex + 1; index < previous.Count; index++)
        {
            var entry = previous[index];
            decorationsByCommit.TryGetValue(entry.Id, out var decorations);
            if (!BranchColors.HaveSameBranches(entry.Decorations, decorations ?? []))
            {
                // The colours of the lanes below were chosen by this branch name, so the old rows would be stale.
                return null;
            }

            if (decorations is not null)
            {
                if (!entry.Decorations.SequenceEqual(decorations))
                {
                    entry = entry with { Decorations = decorations };
                }
            }
            else if (entry.Decorations.Count > 0)
            {
                if (!entry.Row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Incoming))
                {
                    return null;
                }

                entry = entry with { Decorations = [] };
            }

            result.Add(entry);
        }

        return result;
    }

    private static bool SameLayout(GraphRow old, GraphRow row) =>
        old.LaneFingerprint != 0
        && old.Lane == row.Lane
        && old.LaneCount == row.LaneCount
        && old.LaneFingerprint == row.LaneFingerprint
        && old.Edges.SequenceEqual(row.Edges);

    private Dictionary<ObjectId, int> IndexById()
    {
        if (_indexById is null)
        {
            _indexById = new Dictionary<ObjectId, int>(previous.Count);
            for (var index = 0; index < previous.Count; index++)
            {
                _indexById[previous[index].Id] = index;
            }
        }

        return _indexById;
    }
}
