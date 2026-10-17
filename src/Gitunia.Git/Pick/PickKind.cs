namespace Gitunia.Git.Pick;

/// <summary>Which way commits are carried over: applied again on HEAD, or undone by new commits.</summary>
public enum PickKind
{
    /// <summary><c>git cherry-pick</c>.</summary>
    CherryPick,

    /// <summary><c>git revert</c>.</summary>
    Revert,
}
