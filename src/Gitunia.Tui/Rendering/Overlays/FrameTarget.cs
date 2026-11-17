using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// A clickable stretch of a frame: a hint, an action row or a list row. A click on it runs <see cref="Action"/> as if its
/// key had been pressed, or, for a row, the mouse view named in <see cref="View"/> with <see cref="Target"/>.
/// </summary>
/// <param name="Line">The frame line (0 is the top border).</param>
/// <param name="Start">The first frame column (0 is the left border).</param>
/// <param name="End">The column after the last one.</param>
/// <param name="Action">The action a click runs; null when <paramref name="View"/> decides.</param>
/// <param name="View">The mouse view of a row whose click is resolved in the mouse map, null for an action.</param>
/// <param name="Target">The number the mouse view's binding takes (a row), null for none.</param>
public sealed record FrameTarget(
    int Line,
    int Start,
    int End,
    ActionId? Action = null,
    string? View = null,
    int? Target = null)
{
    /// <summary>True when the frame position is inside the target.</summary>
    /// <param name="line">The frame line.</param>
    /// <param name="column">The frame column.</param>
    public bool Contains(int line, int column) => line == Line && column >= Start && column < End;
}
