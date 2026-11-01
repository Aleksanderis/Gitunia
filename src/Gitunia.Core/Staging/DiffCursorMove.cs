namespace Gitunia.Core.Staging;

/// <summary>Where the diff cursor goes once a staged hunk or range has been read again.</summary>
/// <param name="HunkIndex">The hunk the cursor was in (the one it moves to when that hunk was staged), -1 when the diff
/// had none.</param>
/// <param name="ChangesBefore">For a Visual range: the changed rows before it, the cursor then goes to the change with
/// this number; -1 for a hunk.</param>
public sealed record DiffCursorMove(int HunkIndex, int ChangesBefore);
