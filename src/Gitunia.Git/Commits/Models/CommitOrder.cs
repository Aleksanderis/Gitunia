namespace Gitunia.Git.Commits.Models;

public enum CommitOrder
{
    /// <summary>No commit appears before all of its children, and lines of history stay together (best graph).</summary>
    Topological,

    /// <summary>No commit appears before its children, otherwise by commit date.</summary>
    ByDate,
}
