namespace Gitunia.Git.Pick;

/// <summary>What a cherry-pick or revert call did.</summary>
public enum PickOutcome
{
    /// <summary>Every commit was applied and committed.</summary>
    Completed,

    /// <summary>Nothing changed: HEAD already had the changes of every commit (cherry-pick only).</summary>
    NothingToApply,

    /// <summary>Git stopped on conflicts; the operation is in progress.</summary>
    Conflicted,
}
