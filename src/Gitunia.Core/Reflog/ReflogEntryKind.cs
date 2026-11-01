namespace Gitunia.Core.Reflog;

/// <summary>What a reflog entry is used for.</summary>
public enum ReflogEntryKind
{
    /// <summary>Select the entry's commit in the graph.</summary>
    SelectCommit,

    /// <summary>Reset the current branch to the entry (the reset flow).</summary>
    Reset,

    /// <summary>Create a branch at the entry and switch to it.</summary>
    CreateBranch,

    /// <summary>Check the entry out with a detached HEAD.</summary>
    CheckoutDetached,
}
