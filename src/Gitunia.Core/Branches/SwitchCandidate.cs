namespace Gitunia.Core.Branches;

/// <summary>One line of the switch picker.</summary>
/// <param name="Kind">What checking it out does.</param>
/// <param name="Label">The text shown and matched (the branch name, or a description of the detached commit).</param>
/// <param name="Target">The branch name (<c>origin/topic</c> for a remote one) or the commit hash.</param>
/// <param name="CheckedOutIn">For a local branch held by another worktree, that worktree's path; otherwise null.</param>
/// <param name="IsCurrent">The branch HEAD is on (nothing to switch).</param>
public sealed record SwitchCandidate(
    SwitchTargetKind Kind,
    string Label,
    string Target,
    string? CheckedOutIn,
    bool IsCurrent);
