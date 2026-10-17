namespace Gitunia.Git.Merge;

/// <summary>
/// The typed result of a merge. A stop on conflicts is a result (<see cref="MergeKind.Conflicted"/>), not a failure:
/// for a normal merge the operation is then in progress (banner), for a squash only the files are marked unmerged.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="Head">The commit HEAD points at afterwards.</param>
/// <param name="OldHead">The commit HEAD pointed at before, for the undo hint.</param>
public sealed record MergeResult(MergeKind Kind, string? Head, string? OldHead);
