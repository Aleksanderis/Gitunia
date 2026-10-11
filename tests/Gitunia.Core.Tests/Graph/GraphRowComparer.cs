using Gitunia.Core.Graph;

namespace Gitunia.Core.Tests.Graph;

/// <summary>Value comparison of graph rows (the row record compares its edge list by reference).</summary>
public static class GraphRowComparer
{
    public static bool AreEqual(IReadOnlyList<GraphRow> left, IReadOnlyList<GraphRow> right) =>
        left.Count == right.Count && left.Zip(right).All(pair => AreEqual(pair.First, pair.Second));

    public static bool AreEqual(GraphRow left, GraphRow right) =>
        left.Id == right.Id && left.Lane == right.Lane && left.LaneCount == right.LaneCount && left.Edges.SequenceEqual(right.Edges);
}
