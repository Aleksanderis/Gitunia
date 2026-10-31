namespace Gitunia.Git.Stash.Models;

/// <summary>What restoring one file from a stash would do to the working tree.</summary>
public enum StashRestoreState
{
    /// <summary>The stash is no longer in the stack; nothing can be restored.</summary>
    Missing,

    /// <summary>The stash holds no copy of the file (it deletes it, or the path is not in it).</summary>
    NotInStash,

    /// <summary>The working-tree file already has the stashed content.</summary>
    Identical,

    /// <summary>The file does not exist in the working tree, so restoring creates it and loses nothing.</summary>
    Creates,

    /// <summary>The working-tree file differs from the stashed one and would be replaced.</summary>
    Overwrites,
}
