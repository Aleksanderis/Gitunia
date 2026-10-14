namespace Gitunia.Git.Status.Models;

/// <summary>Which porcelain v2 record an entry came from.</summary>
public enum StatusEntryKind
{
    /// <summary>A changed tracked path (record type 1).</summary>
    Ordinary,

    /// <summary>A path renamed in the index or working tree (record type 2, score letter R).</summary>
    Renamed,

    /// <summary>A path copied in the index or working tree (record type 2, score letter C).</summary>
    Copied,

    /// <summary>A path with an unresolved merge conflict (record type u).</summary>
    Unmerged,

    /// <summary>A path git does not track (record type ?).</summary>
    Untracked,

    /// <summary>A path matched by an ignore rule (record type !).</summary>
    Ignored,
}
