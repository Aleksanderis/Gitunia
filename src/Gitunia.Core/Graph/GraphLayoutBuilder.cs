using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Assigns lanes to commits fed in topological order (children before parents), one row at a time, so a partial
/// log can be shown while the rest is still loading. The result depends only on the sequence of commits, never on
/// how it was split into <see cref="Append"/> calls. Rows are immutable; the builder only keeps the open lanes.
/// When the commit of the checked-out branch is given, lane 0 is reserved for its first-parent chain: no other commit
/// takes lane 0 until the chain ends, and every chain commit sits in lane 0, so the current branch is a straight
/// shaft on the left; a tip directly above the chain (a branch ahead of HEAD) takes lane 0 too. Without it (detached HEAD, no commits) the leftmost free lane is used.
/// </summary>
public sealed class GraphLayoutBuilder
{
    // The entry at lanes[index] is the commit the lane is waiting for; the default (invalid) id marks a free lane.
    private readonly List<ObjectId> _lanes = [];
    private readonly List<GraphRow> _rows = [];

    // The next commit of the reserved first-parent chain of HEAD (lane 0); the default (invalid) id when there is no chain
    // or it has ended. Lane 0 is not handed out to anything else while this is valid.
    private ObjectId _chainNext;

    /// <param name="headCommit">The commit the checked-out branch points at; null for a detached or unborn HEAD.</param>
    public GraphLayoutBuilder(ObjectId? headCommit = null)
    {
        _chainNext = headCommit ?? default;
    }

    /// <summary>All rows laid out so far, in feed order.</summary>
    public IReadOnlyList<GraphRow> Rows => _rows.AsReadOnly();

    /// <summary>Number of lanes currently open below the last row.</summary>
    public int OpenLaneCount => _lanes.Count;

    /// <summary>Lays out the next commit and returns its row.</summary>
    public GraphRow Append(CommitInfo commit)
    {
        var commitId = commit.Id;
        var edges = new List<GraphEdge>(_lanes.Count + commit.Parents.Count);

        var isChain = _chainNext.IsValid && commitId == _chainNext;
        var nodeLane = isChain ? 0 : IndexOfLane(commitId);
        if (nodeLane < 0)
        {
            nodeLane = IsDirectDescendantOfChain(commit) ? 0 : FindFreeLane();
        }

        EnsureLane(nodeLane);

        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (!_lanes[lane].IsValid)
            {
                continue;
            }

            if (_lanes[lane] == commitId)
            {
                edges.Add(new GraphEdge(GraphEdgeKind.Incoming, lane, nodeLane));
                _lanes[lane] = default;
            }
            else
            {
                edges.Add(new GraphEdge(GraphEdgeKind.Through, lane, lane));
            }
        }

        for (var parentIndex = 0; parentIndex < commit.Parents.Count; parentIndex++)
        {
            var parent = commit.Parents[parentIndex];
            int parentLane;
            if (isChain && parentIndex == 0)
            {
                // The chain stays in lane 0 even when another lane already waits for this parent (that lane joins it later).
                parentLane = 0;
                _chainNext = parent;
                _lanes[0] = parent;
            }
            else if (_chainNext.IsValid && parent == _chainNext)
            {
                parentLane = 0;
                EnsureLane(0);
                _lanes[0] = parent;
            }
            else
            {
                parentLane = IndexOfLane(parent);
                if (parentLane < 0)
                {
                    parentLane = parentIndex == 0 ? nodeLane : FindFreeLane();
                    EnsureLane(parentLane);
                    _lanes[parentLane] = parent;
                }
            }

            edges.Add(new GraphEdge(GraphEdgeKind.Outgoing, nodeLane, parentLane));
        }

        if (isChain && commit.Parents.Count == 0)
        {
            _chainNext = default;
        }

        var laneCount = nodeLane + 1;
        foreach (var edge in edges)
        {
            laneCount = Math.Max(laneCount, Math.Max(edge.FromLane, edge.ToLane) + 1);
        }

        TrimFreeLanes();
        var row = new GraphRow(commitId, nodeLane, laneCount, edges.ToArray(), FingerprintLanes());
        _rows.Add(row);
        return row;
    }

    /// <summary>Lays out several commits and returns just their rows.</summary>
    public IReadOnlyList<GraphRow> AppendRange(IEnumerable<CommitInfo> commits)
    {
        var added = new List<GraphRow>();
        foreach (var commit in commits)
        {
            added.Add(Append(commit));
        }

        return added;
    }

    private int IndexOfLane(ObjectId commitId)
    {
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (_lanes[lane] == commitId)
            {
                return lane;
            }
        }

        return -1;
    }

    // A tip no lane waits for whose first parent is the next commit of the reserved chain (a branch that is ahead of
    // HEAD, such as origin/main) continues the chain upwards: it takes lane 0 and the shaft stays straight. Only while
    // lane 0 is still empty, so the first such tip gets it and later ones bend in from the right.
    private bool IsDirectDescendantOfChain(CommitInfo commit) =>
        _chainNext.IsValid
        && commit.Parents.Count > 0
        && commit.Parents[0] == _chainNext
        && (_lanes.Count == 0 || !_lanes[0].IsValid);

    private void EnsureLane(int lane)
    {
        while (_lanes.Count <= lane)
        {
            _lanes.Add(default);
        }
    }

    // Leftmost free lane, or the next new one; lane 0 is skipped while it is reserved for the chain of HEAD. Only the
    // lane of the commit being laid out can end in a row, and a later parent may reuse it: its line simply continues
    // straight down.
    private int FindFreeLane()
    {
        var first = _chainNext.IsValid ? 1 : 0;
        for (var lane = first; lane < _lanes.Count; lane++)
        {
            if (!_lanes[lane].IsValid)
            {
                return lane;
            }
        }

        return Math.Max(_lanes.Count, first);
    }

    // FNV-style mix over (lane index, waited-for commit) of the occupied lanes.
    private ulong FingerprintLanes()
    {
        var hash = 14695981039346656037UL;
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (_lanes[lane].IsValid)
            {
                hash = (hash ^ _lanes[lane].Hash64 ^ ((ulong)(lane + 1) << 56)) * 1099511628211UL;
            }
        }

        if (_chainNext.IsValid)
        {
            // The reservation shapes the rows below (even when lane 0 is still empty), so it is part of what must match.
            hash = (hash ^ 0x9E3779B97F4A7C15UL) * 1099511628211UL;
            if (_lanes.Count == 0 || !_lanes[0].IsValid)
            {
                hash = (hash ^ _chainNext.Hash64) * 1099511628211UL;
            }
        }

        return hash;
    }

    private void TrimFreeLanes()
    {
        var count = _lanes.Count;
        while (count > 0 && !_lanes[count - 1].IsValid)
        {
            count--;
        }

        _lanes.RemoveRange(count, _lanes.Count - count);
    }
}
