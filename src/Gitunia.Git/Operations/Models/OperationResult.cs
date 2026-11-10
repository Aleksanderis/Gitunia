namespace Gitunia.Git.Operations.Models;

/// <summary>
/// The typed result of a call that runs or advances a multi-step operation (merge, rebase, cherry-pick, revert).
/// A stop on conflicts is a result, not a failure; every other failure throws <c>GitCommandFailedException</c>.
/// </summary>
/// <param name="Outcome">Completed, stopped on conflicts, or emptied (dropped as empty).</param>
/// <param name="Head">The commit HEAD points at afterwards, null on an unborn branch.</param>
public sealed record OperationResult(OperationOutcome Outcome, string? Head);
