namespace Gitunia.Core.Graph;

/// <summary>
/// One line segment in a graph row. For <see cref="GraphEdgeKind.Through"/> both lanes are equal unless the layout shifts lanes (#1228); for
/// <see cref="GraphEdgeKind.Incoming"/> <paramref name="ToLane"/> is the node lane; for
/// <see cref="GraphEdgeKind.Outgoing"/> <paramref name="FromLane"/> is the node lane. <paramref name="Color"/> is the
/// colour slot (see <see cref="BranchColors"/>) of the lane the line runs on: the lane it comes from for a through
/// or incoming line, the lane it goes to for an outgoing one.
/// </summary>
public readonly record struct GraphEdge(GraphEdgeKind Kind, int FromLane, int ToLane, int Color = 0);
