namespace Gitunia.Tui.App.WorkingTree;

/// <summary>The outcome of reading what to stage from the Changes view diff: the request, or why there is none.</summary>
/// <param name="Request">What to stage, null when refused.</param>
/// <param name="HunkIndex">The hunk the cursor was in (the one the cursor moves to after the operation), -1 when the diff has none.</param>
/// <param name="ChangesBefore">For a Visual range: the changed rows before it. After the operation the cursor goes to the change with this number; -1 for a hunk.</param>
/// <param name="Notice">Why nothing can be staged, empty when <paramref name="Request"/> is set.</param>
public sealed record DiffStagingCapture(DiffStagingRequest? Request, int HunkIndex, string Notice, int ChangesBefore = -1)
{
    public static DiffStagingCapture Refused(string notice) => new(null, -1, notice);
}
