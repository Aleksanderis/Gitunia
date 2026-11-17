using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>A footer row and the columns each of its hints took.</summary>
/// <param name="Line">The row, border included.</param>
/// <param name="Spans">One entry per hint, in the builder's columns (inside the left border).</param>
public sealed record HintsLine(StyledLine Line, IReadOnlyList<HintSpan> Spans);
