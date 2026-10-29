namespace Gitunia.Tui.Rendering.Styling;

/// <summary>The roles a <see cref="KeyedChip"/> is drawn in: the pane chips and the tab row's chips differ in colour
/// only.</summary>
/// <param name="Plain">Role of a chip without a key.</param>
/// <param name="Edge">Role of the padding or bracket columns of a chip with a key.</param>
/// <param name="Key">Role of the key text.</param>
/// <param name="Description">Role of the description.</param>
public sealed record ChipStyle(SegmentRole Plain, SegmentRole Edge, SegmentRole Key, SegmentRole Description)
{
    /// <summary>Chips on pane titles and change groups: drawn like the tabs.</summary>
    public static ChipStyle Pane { get; } = new(
        SegmentRole.RepositoryTab,
        SegmentRole.RepositoryTab,
        SegmentRole.RepositoryTabKey,
        SegmentRole.RepositoryTab);

    /// <summary>Chips on the tab row: the key like a status bar hint, the description plain; in the ASCII tier the
    /// whole chip is a button.</summary>
    public static ChipStyle Bar(bool isAscii) => new(
        SegmentRole.BarButton,
        SegmentRole.BarButton,
        SegmentRole.StatusKey,
        isAscii ? SegmentRole.BarButton : SegmentRole.Status);
}
