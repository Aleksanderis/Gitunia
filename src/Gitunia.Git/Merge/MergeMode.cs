namespace Gitunia.Git.Merge;

/// <summary>How a merge integrates the target into the current branch.</summary>
public enum MergeMode
{
    /// <summary>Fast-forward when possible, otherwise a merge commit.</summary>
    Default,

    /// <summary>Always a merge commit (<c>--no-ff</c>).</summary>
    NoFastForward,

    /// <summary>The changes are staged; no commit is made and no merge is in progress (<c>--squash</c>).</summary>
    Squash,
}
