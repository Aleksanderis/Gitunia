namespace Gitunia.Core.Running;

/// <summary>
/// What a tab is busy with. One operation of a kind runs per tab at a time. A feature that moves to the effect pipeline
/// adds its kind here (in alphabetical order) and derives its effects from <see cref="OperationEffect"/>.
/// </summary>
public enum RunningOperationKind
{
    /// <summary>A branch operation: create, switch, rename, delete, upstream or fast-forward.</summary>
    Branches,

    /// <summary>A remote management operation: add, remove, rename, set the URL, or reading the remotes for the prompt.</summary>
    Remotes,

    /// <summary>A stash operation: push, apply, pop, drop, or restoring one file of a stash.</summary>
    Stash,

    /// <summary>A fetch, pull, push or remote branch delete, or reading the remotes and comparing branches for their questions.</summary>
    Sync,

    /// <summary>A tag operation: create, delete, push, delete on a remote, or reading the remotes for the prompt.</summary>
    Tags,
}
