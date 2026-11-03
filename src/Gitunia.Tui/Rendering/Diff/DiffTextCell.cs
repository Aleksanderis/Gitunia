namespace Gitunia.Tui.Rendering.Diff;

/// <summary>The text of one diff row on one screen line.</summary>
/// <param name="Row">The document row.</param>
/// <param name="LineStart">
/// The column of the text area where the side of the row starts (0, or the new side of the side-by-side view).
/// </param>
/// <param name="Prefix">Columns of line numbers and marker before the text, from <paramref name="LineStart"/>.</param>
/// <param name="First">The column of the row's shown text (tabs expanded) where this line's part begins.</param>
/// <param name="End">The column of the row's shown text where this line's part ends (not included).</param>
/// <param name="Segment">Which screen line of the row this is (0 unless the row wraps).</param>
/// <param name="SegmentCount">How many screen lines the row takes on this side.</param>
public readonly record struct DiffTextCell(
    int Row,
    int LineStart,
    int Prefix,
    int First,
    int End,
    int Segment,
    int SegmentCount);
