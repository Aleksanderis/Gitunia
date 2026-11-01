namespace Gitunia.Core.Stash;

/// <summary>How the stash stack changed the shown stash.</summary>
public enum StashViewChangeKind
{
    /// <summary>The stash is in the stack as shown.</summary>
    Unchanged,

    /// <summary>The stash left the stack (dropped, popped, or changed by another process).</summary>
    Gone,

    /// <summary>The stash is still there under another name (a newer stash pushed it down).</summary>
    Renamed,
}
