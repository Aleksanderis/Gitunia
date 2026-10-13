namespace Gitunia.Core.Graph;

/// <summary>
/// One line segment in a graph row. For <see cref="GraphEdgeKind.Through"/> both lanes are equal; for
/// <see cref="GraphEdgeKind.Incoming"/> <paramref name="ToLane"/> is the node lane; for
/// <see cref="GraphEdgeKind.Outgoing"/> <paramref name="FromLane"/> is the node lane.
/// </summary>
public readonly record struct GraphEdge(GraphEdgeKind Kind, int FromLane, int ToLane);
