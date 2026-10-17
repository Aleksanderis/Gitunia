namespace Gitunia.Git.Pick;

/// <summary>
/// The typed result of a cherry-pick or revert. A stop on conflicts is a result (<see cref="PickOutcome.Conflicted"/>),
/// not a failure: the operation is then in progress (banner, continue, abort, skip).
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Head">The commit HEAD points at afterwards.</param>
/// <param name="OldHead">The commit HEAD pointed at before, for the undo hint.</param>
public sealed record PickResult(PickOutcome Outcome, string? Head, string? OldHead);
