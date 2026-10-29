using Gitunia.Core.Graph;

namespace Gitunia.Core.Tests.Graph;

/// <summary>Value comparison of graph rows (the row record compares its edge list by reference).</summary>
public static class GraphRowComparer
{
    public static bool AreEqual(IReadOnlyList<GraphRow> left, IReadOnlyList<GraphRow> right, bool ignoreColors = false) =>
        left.Count == right.Count && left.Zip(right, (a, b) => AreEqual(a, b, ignoreColors)).All(equal => equal);

    public static bool AreEqual(GraphRow left, GraphRow right, bool ignoreColors = false) =>
        left.Id == right.Id
        && left.Lane == right.Lane
        && left.LaneCount == right.LaneCount
        && (ignoreColors
            ? left.Edges.Select(edge => edge with { Color = 0 }).SequenceEqual(right.Edges.Select(edge => edge with { Color = 0 }))
            : left.Edges.SequenceEqual(right.Edges));
}
