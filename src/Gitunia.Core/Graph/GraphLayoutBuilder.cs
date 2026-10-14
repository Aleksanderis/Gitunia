using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Assigns lanes to commits fed in topological order (children before parents), one row at a time, so a partial
/// log can be shown while the rest is still loading. The result depends only on the sequence of commits, never on
/// how it was split into <see cref="Append"/> calls. Rows are immutable; the builder only keeps the open lanes.
/// </summary>
public sealed class GraphLayoutBuilder
{
    // The entry at lanes[index] is the commit the lane is waiting for; the default (invalid) id marks a free lane.
    private readonly List<ObjectId> _lanes = [];
    private readonly List<GraphRow> _rows = [];

    /// <summary>All rows laid out so far, in feed order.</summary>
    public IReadOnlyList<GraphRow> Rows => _rows.AsReadOnly();

    /// <summary>Number of lanes currently open below the last row.</summary>
    public int OpenLaneCount => _lanes.Count;

    /// <summary>Lays out the next commit and returns its row.</summary>
    public GraphRow Append(CommitInfo commit)
    {
        var commitId = commit.Id;
        var edges = new List<GraphEdge>(_lanes.Count + commit.Parents.Count);

        var nodeLane = IndexOfLane(commitId);
        if (nodeLane < 0)
        {
            nodeLane = FindFreeLane();
            if (nodeLane == _lanes.Count)
            {
                _lanes.Add(default);
            }
        }

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
            var parentLane = IndexOfLane(parent);
            if (parentLane < 0)
            {
                parentLane = parentIndex == 0 ? nodeLane : FindFreeLane();
                if (parentLane == _lanes.Count)
                {
                    _lanes.Add(default);
                }

                _lanes[parentLane] = parent;
            }

            edges.Add(new GraphEdge(GraphEdgeKind.Outgoing, nodeLane, parentLane));
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

    // Leftmost free lane, or the next new one. A commit has at most one lane waiting for it (a parent is only added
    // when no lane already waits for it), so the only lane that can end in a row is the node lane itself, which a
    // later parent may reuse: its line simply continues straight down.
    private int FindFreeLane()
    {
        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (!_lanes[lane].IsValid)
            {
                return lane;
            }
        }

        return _lanes.Count;
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
