using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>One logical line of the detail pane. It is cut to the pane width only when drawn, so the content does not depend on the width.</summary>
/// <param name="Segments">The pieces, left to right: for a file row the change mark, then the path as the full-path
/// style shows it.</param>
/// <param name="File">The path of a file row on the Changes tab, so the row can be drawn in either path style at the
/// width it gets; null on any other row.</param>
public sealed record DetailRow(IReadOnlyList<StyledSegment> Segments, FileRowPath? File = null);
