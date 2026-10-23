using Gitunia.Core.IssueTracker.Models;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>One logical line of the detail pane. It is cut to the pane width only when drawn, so the content does not depend on the width.</summary>
/// <param name="Segments">The pieces, left to right: for a file row the change mark, then the path as the full-path
/// style shows it.</param>
/// <param name="File">The path of a file row on the Changes tab, so the row can be drawn in either path style at the
/// width it gets; null on any other row and on the file rows of the tree style.</param>
/// <param name="FileIndex">Index in <see cref="DetailContent.Files"/> of the file this row shows, -1 on any other
/// row.</param>
/// <param name="Folder">The path of the folder a folder row of the tree style stands for, null on any other
/// row.</param>
/// <param name="References">The tracker links of a line of the commit message, left to right (positions are in the
/// message text, without the indent); null on any other row.</param>
public sealed record DetailRow(
    IReadOnlyList<StyledSegment> Segments,
    FileRowPath? File = null,
    int FileIndex = -1,
    string? Folder = null,
    IReadOnlyList<IssueReference>? References = null);
