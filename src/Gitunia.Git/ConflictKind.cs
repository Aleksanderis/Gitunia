namespace Gitunia.Git;

/// <summary>Who changed what in an unmerged path; "us" is the current branch, "them" the branch being merged.</summary>
public enum ConflictKind
{
    /// <summary>Not a conflict.</summary>
    None,

    /// <summary>DD: both deleted.</summary>
    BothDeleted,

    /// <summary>AU: added by us.</summary>
    AddedByUs,

    /// <summary>UD: deleted by them.</summary>
    DeletedByThem,

    /// <summary>UA: added by them.</summary>
    AddedByThem,

    /// <summary>DU: deleted by us.</summary>
    DeletedByUs,

    /// <summary>AA: both added.</summary>
    BothAdded,

    /// <summary>UU: both modified.</summary>
    BothModified,
}
