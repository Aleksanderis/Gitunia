using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>One logical line of the detail pane. It is cut to the pane width only when drawn, so the content does not depend on the width.</summary>
/// <param name="Segments">The pieces, left to right.</param>
public sealed record DetailRow(IReadOnlyList<StyledSegment> Segments);
