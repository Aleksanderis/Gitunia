namespace Gitunia.Git.Refs.Models;

/// <summary>What HEAD points at.</summary>
/// <param name="Kind">Branch, detached or unborn.</param>
/// <param name="BranchName">Short branch name; null when detached.</param>
/// <param name="CommitHash">Full commit hash; null when unborn.</param>
public sealed record HeadState(HeadKind Kind, string? BranchName, string? CommitHash);
