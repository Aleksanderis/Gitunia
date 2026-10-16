namespace Gitunia.Git.Stash.Models;

/// <summary>The result of a stash write.</summary>
/// <param name="Outcome">How it ended.</param>
/// <param name="CommitHash">The stash commit that was created, applied, popped or dropped; null when there is none.</param>
public sealed record StashResult(StashOutcome Outcome, string? CommitHash);
