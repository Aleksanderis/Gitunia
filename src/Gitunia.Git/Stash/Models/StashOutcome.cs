namespace Gitunia.Git.Stash.Models;

/// <summary>How a stash write ended; git failures other than these are exceptions.</summary>
public enum StashOutcome
{
    /// <summary>The stash was created, or applied without conflicts (a pop also dropped it).</summary>
    Done,

    /// <summary>
    /// Push: there was nothing to stash (no local changes, or only untracked files without the untracked option).
    /// </summary>
    NothingToStash,

    /// <summary>Apply or pop: the changes were applied with conflicts; the stash is kept.</summary>
    Conflicted,

    /// <summary>The entry is no longer in the stash stack (another process changed it); nothing was done.</summary>
    Missing,
}
