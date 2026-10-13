namespace Gitunia.Core.Graph;

/// <summary>What a <see cref="GraphEdge"/> connects within one graph row.</summary>
public enum GraphEdgeKind
{
    /// <summary>A lane that passes straight through the row, from its top to its bottom in the same lane.</summary>
    Through,

    /// <summary>A lane from the top of the row ending at the commit's node (a child waiting for this commit).</summary>
    Incoming,

    /// <summary>A line from the commit's node to the bottom of the row, towards a parent.</summary>
    Outgoing,
}
