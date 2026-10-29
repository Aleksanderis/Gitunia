using System.Collections.Frozen;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Graph;

/// <summary>
/// Assigns lanes to commits fed in topological order (children before parents), one row at a time, so a partial
/// log can be shown while the rest is still loading. The result depends only on the sequence of commits, never on
/// how it was split into <see cref="Append"/> calls. Rows are immutable; the builder only keeps the open lanes.
/// A commit no lane waits for (a branch tip) takes the leftmost free lane. The log is newest first, so the branch
/// with the newest tip ends up leftmost, the next newest right of it, and so on; the checked-out branch gets no
/// lane of its own (#757, which replaced the lane 0 reservation of #308).
/// </summary>
public sealed class GraphLayoutBuilder
{
    /// <summary>
    /// Rows within which two different branch tips avoid sharing a colour while a free one remains: about one screen.
    /// </summary>
    public const int NearRowCount = 50;

    // The entry at lanes[index] is the commit the lane is waiting for; the default (invalid) id marks a free lane.
    private readonly List<ObjectId> _lanes = [];

    // The colour slot of each lane (see BranchColors); only meaningful while the lane is occupied.
    private readonly List<int> _laneSlots = [];

    // Whether the lane was started by a second parent whose branch name chose its colour (GraphRow.ColorChosenByName).
    private readonly List<bool> _laneNamedByParent = [];

    private readonly List<GraphRow> _rows = [];

    // The slot each branch name got: a branch keeps one colour for the whole graph, local and remote alike.
    private readonly Dictionary<string, int> _namedSlots = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<ObjectId, string> _branchTips;

    // The other branch names of a commit that carries several (the first one is in _branchTips), see BranchSlot.
    private readonly IReadOnlyDictionary<ObjectId, IReadOnlyList<string>> _extraNames;

    // The branch tips laid out within the last NearRowCount rows (row and slot), so a new branch does not take a
    // colour a branch seen on the same screen already has, even when that branch's lane is closed or it has no lane
    // of its own (#807). Oldest first.
    private readonly Queue<RecentTip> _recentTips = new();
    private readonly int _colorCount;

    /// <param name="branchTips">
    /// The branch name of every commit that carries a branch ref (<see cref="BranchColors.TipsOf"/>), so a lane gets
    /// the colour of its branch from its first row on; null when not known (every lane is then coloured by its commit).
    /// </param>
    /// <param name="colorCount">
    /// Distinct colours the palette has (at most <see cref="BranchColors.SlotCount"/>): lanes open at the same time
    /// get different colours while one is free, so a short palette (16 colours, a small theme) never repeats one early.
    /// </param>
    /// <param name="extraBranchNames">
    /// The further branch names of the commits that carry several (<see cref="BranchColors.ExtraNamesOf"/>): each
    /// name gets a chip colour of its own (#849), none of them colours a lane.
    /// </param>
    public GraphLayoutBuilder(
        IReadOnlyDictionary<ObjectId, string>? branchTips = null,
        int colorCount = BranchColors.SlotCount,
        IReadOnlyDictionary<ObjectId, IReadOnlyList<string>>? extraBranchNames = null)
    {
        _extraNames = extraBranchNames ?? FrozenDictionary<ObjectId, IReadOnlyList<string>>.Empty;
        _colorCount = Math.Clamp(colorCount, 1, BranchColors.SlotCount);
        _branchTips = branchTips ?? FrozenDictionary<ObjectId, string>.Empty;
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
        while (_recentTips.Count > 0 && _rows.Count - _recentTips.Peek().Row > NearRowCount)
        {
            _recentTips.Dequeue();
        }

        var nodeLane = NodeLaneOf(commitId, out var isTip);
        var nodeColor = _laneSlots[nodeLane];
        var isColorChosenByName = isTip;

        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (!_lanes[lane].IsValid)
            {
                continue;
            }

            if (_lanes[lane] == commitId)
            {
                edges.Add(new GraphEdge(GraphEdgeKind.Incoming, lane, nodeLane, _laneSlots[lane]));
                isColorChosenByName |= _laneNamedByParent[lane];
                _laneNamedByParent[lane] = false;
                _lanes[lane] = default;
            }
            else
            {
                edges.Add(new GraphEdge(GraphEdgeKind.Through, lane, lane, _laneSlots[lane]));
            }
        }

        for (var parentIndex = 0; parentIndex < commit.Parents.Count; parentIndex++)
        {
            var parent = commit.Parents[parentIndex];
            int parentLane;
            if (parentIndex == 0)
            {
                // The first parent continues the node's own lane even when another lane already waits for it: every
                // branch keeps its lane down to the commit it forked from, where the lanes join (#759).
                parentLane = nodeLane;
                _laneSlots[parentLane] = nodeColor;
                _lanes[parentLane] = parent;
            }
            else
            {
                parentLane = IndexOfLane(parent);
                if (parentLane < 0)
                {
                    parentLane = FindFreeLane();
                    EnsureLane(parentLane);
                    _laneSlots[parentLane] = SlotForNewLane(parent);
                    _lanes[parentLane] = parent;
                    _laneNamedByParent[parentLane] = true;
                }
            }

            edges.Add(new GraphEdge(GraphEdgeKind.Outgoing, nodeLane, parentLane, _laneSlots[parentLane]));
        }

        // Lanes and edges are final here, so the chip colours avoid the lane colours of this row as well.
        var labelSlots = LabelSlotsOf(commitId, isTip, nodeColor);

        var laneCount = nodeLane + 1;
        foreach (var edge in edges)
        {
            laneCount = Math.Max(laneCount, Math.Max(edge.FromLane, edge.ToLane) + 1);
        }

        TrimFreeLanes();
        var row = new GraphRow(
            commitId,
            nodeLane,
            laneCount,
            edges.ToArray(),
            FingerprintLanes(),
            nodeColor,
            isColorChosenByName,
            labelSlots);
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

    // The lane of the commit's node: the leftmost lane that waits for it, so where several lanes meet the leftmost one
    // (the newest tip's) goes on to the bottom and the others bend into it; a commit no lane waits for starts a lane.
    private int NodeLaneOf(ObjectId commitId, out bool isTip)
    {
        var nodeLane = IndexOfLane(commitId);
        isTip = nodeLane < 0;
        if (!isTip)
        {
            return nodeLane;
        }

        nodeLane = FindFreeLane();
        EnsureLane(nodeLane);
        _laneSlots[nodeLane] = SlotForNewLane(commitId);

        return nodeLane;
    }

    // The chip colour of every branch name on the commit (#849): a branch has one colour for the whole graph, the same
    // for its local and its remote-tracking ref, and different branches differ while a colour is free, even when they
    // share a lane. The name that started the lane of this row has the lane colour; every other name takes the colour of
    // its name, moved on to one no open lane and no chip of the last rows uses. All of them are remembered as recent tips.
    private BranchSlot[]? LabelSlotsOf(ObjectId commitId, bool isTip, int nodeColor)
    {
        if (!_branchTips.TryGetValue(commitId, out var primary))
        {
            return null;
        }

        var slots = new List<BranchSlot> { new(primary, isTip ? nodeColor : SlotForName(primary)) };
        _recentTips.Enqueue(new RecentTip(_rows.Count, slots[0].Slot));
        if (_extraNames.TryGetValue(commitId, out var extras))
        {
            foreach (var name in extras)
            {
                var slot = SlotForName(name);
                slots.Add(new BranchSlot(name, slot));
                _recentTips.Enqueue(new RecentTip(_rows.Count, slot));
            }
        }

        return [.. slots];
    }

    private int SlotForName(string name)
    {
        if (!_namedSlots.TryGetValue(name, out var slot))
        {
            slot = FreeSlotFrom(BranchColors.PreferredSlot(name, _colorCount));
            _namedSlots[name] = slot;
        }

        return slot;
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

    private void EnsureLane(int lane)
    {
        while (_lanes.Count <= lane)
        {
            _lanes.Add(default);
            _laneSlots.Add(0);
            _laneNamedByParent.Add(false);
        }
    }

    // The slot of a lane that starts at this commit: its branch's slot when it is a branch tip, else one derived from
    // the commit, in both cases moved on to the next slot no open lane of another branch is using.
    private int SlotForNewLane(ObjectId commitId)
    {
        if (!_branchTips.TryGetValue(commitId, out var name))
        {
            return FreeSlotFrom((int)(commitId.Hash64 % (uint)_colorCount));
        }

        return SlotForName(name);
    }

    private int FreeSlotFrom(int preferred)
    {
        for (var offset = 0; offset < _colorCount; offset++)
        {
            var slot = (preferred + offset) % _colorCount;
            if (!IsSlotInUse(slot))
            {
                return slot;
            }
        }

        return preferred;
    }

    private bool IsSlotInUse(int slot)
    {
        foreach (var tip in _recentTips)
        {
            if (tip.Slot == slot)
            {
                return true;
            }
        }

        for (var lane = 0; lane < _lanes.Count; lane++)
        {
            if (_lanes[lane].IsValid && _laneSlots[lane] == slot)
            {
                return true;
            }
        }

        return false;
    }

    // Leftmost free lane, or the next new one. Only the lane of the commit being laid out can end in a row, and a later
    // parent may reuse it: its line simply continues straight down.
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
                hash = (hash ^ _lanes[lane].Hash64 ^ ((ulong)(lane + 1) << 56) ^ ((ulong)(_laneSlots[lane] + 1) << 48))
                    * 1099511628211UL;
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
        _laneSlots.RemoveRange(count, _laneSlots.Count - count);
        _laneNamedByParent.RemoveRange(count, _laneNamedByParent.Count - count);
    }
}
