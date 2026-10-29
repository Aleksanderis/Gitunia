using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Tests.Graph;

/// <summary>
/// Replays a layout row by row and checks that its edges describe exactly the commit topology: every parent link
/// is an outgoing edge whose lane is picked up again by the parent's incoming edge, lanes never carry two different
/// commits.
/// </summary>
public static class GraphTopologyValidator
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<CommitInfo> commits, IReadOnlyList<GraphRow> rows)
    {
        var problems = new List<string>();
        if (commits.Count != rows.Count)
        {
            problems.Add($"{commits.Count} commits but {rows.Count} rows");
            return problems;
        }

        var waiting = new Dictionary<int, ObjectId>();
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var commit = commits[index];
            var where = $"row {index}";
            if (row.Id != commit.Id)
            {
                problems.Add($"{where}: wrong commit id");
            }

            var incomingLanes = row.Edges.Where(edge => edge.Kind == GraphEdgeKind.Incoming).Select(edge => edge.FromLane).Order().ToArray();
            var expectedIncoming = waiting.Where(pair => pair.Value == commit.Id).Select(pair => pair.Key).Order().ToArray();
            if (!incomingLanes.SequenceEqual(expectedIncoming))
            {
                problems.Add($"{where}: incoming lanes [{string.Join(",", incomingLanes)}] but lanes waiting for the commit are [{string.Join(",", expectedIncoming)}]");
            }

            if (expectedIncoming.Length > 0 && !expectedIncoming.Contains(row.Lane))
            {
                problems.Add($"{where}: node is not in a lane waiting for it");
            }

            if (waiting.TryGetValue(row.Lane, out var laneOwner) && laneOwner != commit.Id)
            {
                problems.Add($"{where}: node sits in a lane that waits for another commit");
            }

            var throughLanes = row.Edges.Where(edge => edge.Kind == GraphEdgeKind.Through).ToArray();
            var expectedThrough = waiting.Where(pair => pair.Value != commit.Id).Select(pair => pair.Key).Order().ToArray();
            if (throughLanes.Any(edge => edge.FromLane != edge.ToLane)
                || !throughLanes.Select(edge => edge.FromLane).Order().SequenceEqual(expectedThrough))
            {
                problems.Add($"{where}: through lanes do not match the open lanes");
            }

            if (row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Incoming && edge.ToLane != row.Lane)
                || row.Edges.Any(edge => edge.Kind == GraphEdgeKind.Outgoing && edge.FromLane != row.Lane))
            {
                problems.Add($"{where}: incoming/outgoing edges do not meet at the node lane");
            }

            var outgoing = row.Edges.Where(edge => edge.Kind == GraphEdgeKind.Outgoing).ToArray();
            if (outgoing.Length != commit.Parents.Count)
            {
                problems.Add($"{where}: {outgoing.Length} outgoing edges for {commit.Parents.Count} parents");
                return problems;
            }

            var next = new Dictionary<int, ObjectId>();
            foreach (var edge in throughLanes)
            {
                next[edge.ToLane] = waiting[edge.FromLane];
            }

            for (var parentIndex = 0; parentIndex < outgoing.Length; parentIndex++)
            {
                var parent = commit.Parents[parentIndex];
                var targetLane = outgoing[parentIndex].ToLane;
                if (next.TryGetValue(targetLane, out var existing) && existing != parent)
                {
                    problems.Add($"{where}: lane {targetLane} would carry two different commits");
                }

                next[targetLane] = parent;
            }

            var highestLane = row.Edges.Select(edge => Math.Max(edge.FromLane, edge.ToLane)).Append(row.Lane).Max();
            if (row.LaneCount != highestLane + 1 || row.Lane < 0)
            {
                problems.Add($"{where}: lane count {row.LaneCount} does not match highest lane {highestLane}");
            }

            waiting = next;
        }

        return problems;
    }
}
