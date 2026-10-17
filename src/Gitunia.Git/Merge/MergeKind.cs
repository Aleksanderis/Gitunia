namespace Gitunia.Git.Merge;

/// <summary>What a merge call did.</summary>
public enum MergeKind
{
    /// <summary>The branch already contained the target; nothing changed.</summary>
    UpToDate,

    /// <summary>HEAD moved to the target without a new commit.</summary>
    FastForward,

    /// <summary>A merge commit was created.</summary>
    MergeCommit,

    /// <summary>The changes are staged for a commit; HEAD did not move.</summary>
    Squashed,

    /// <summary>Git stopped on conflicts.</summary>
    Conflicted,
}
