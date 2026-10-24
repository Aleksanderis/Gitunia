namespace Gitunia.Core.Conflicts;

/// <summary>
/// What an undo or redo restores: the picks and edited texts of every hunk and the hunk they were made on.
/// </summary>
/// <param name="Picks">The pick of each hunk.</param>
/// <param name="Texts">The edited text of each hunk (set where the pick is <see cref="ConflictPick.Edited"/>).</param>
/// <param name="SelectedIndex">The hunk under the cursor.</param>
public sealed record ConflictSnapshot(
    IReadOnlyList<ConflictPick> Picks,
    IReadOnlyList<string?> Texts,
    int SelectedIndex);
