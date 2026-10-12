using Gitunia.Git;

namespace Gitunia.Core.Graph;

/// <summary>The layout of one commit: its lane (column), the edges drawn in its row and how many lanes the row spans.</summary>
/// <param name="Id">The commit this row belongs to.</param>
/// <param name="Lane">Zero-based lane of the commit's node.</param>
/// <param name="LaneCount">Number of lanes (columns) occupied by the node and edges of this row.</param>
/// <param name="Edges">Edges in a stable order: lanes from the top (through and incoming) by lane, then outgoing in parent order.</param>
/// <param name="LaneFingerprint">Hash of the commits the open lanes wait for below this row; equal fingerprints (with equal following commits) mean equal layout from here on, which lets a refresh reuse the old rows.</param>
public sealed record GraphRow(ObjectId Id, int Lane, int LaneCount, IReadOnlyList<GraphEdge> Edges, ulong LaneFingerprint = 0);
