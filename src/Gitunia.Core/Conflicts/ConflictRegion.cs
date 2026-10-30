namespace Gitunia.Core.Conflicts;

/// <summary>Where one conflict sits in the result pane and how it was resolved.</summary>
/// <param name="Start">Zero-based first result line of the region.</param>
/// <param name="Length">Result lines the region holds; zero when the user emptied it.</param>
/// <param name="Pick">How the region got its lines.</param>
public readonly record struct ConflictRegion(int Start, int Length, ConflictPick Pick)
{
    /// <summary>Zero-based result line after the region's last line.</summary>
    public int End => Start + Length;
}
