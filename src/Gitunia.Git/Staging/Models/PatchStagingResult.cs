namespace Gitunia.Git.Staging.Models;

/// <summary>The result of staging or unstaging chosen lines.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="LineCount">Added and removed lines the applied patch changes, 0 unless <see cref="PatchStagingOutcome.Applied"/>.</param>
public sealed record PatchStagingResult(PatchStagingOutcome Outcome, int LineCount);
