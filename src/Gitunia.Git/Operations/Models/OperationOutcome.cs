namespace Gitunia.Git.Operations.Models;

/// <summary>How a multi-step operation call ended. Shared by continue, abort, skip and the operations that start
/// one.</summary>
public enum OperationOutcome
{
    /// <summary>Git finished the call and nothing is waiting for the user.</summary>
    Completed,

    /// <summary>Git stopped on conflicts: the operation is in progress and the files are marked unmerged.</summary>
    Conflicted,

    /// <summary>
    /// A continue of a cherry-pick or revert whose resolved result equals HEAD: git refuses to commit nothing, so the
    /// commit was dropped (skipped), as a rebase drops a commit that became empty.
    /// </summary>
    Emptied,
}
