using Gitunia.Core.Overlays;

namespace Gitunia.Core.Input;

/// <summary>
/// What the terminal layer knows about the focus when a key or a gesture arrives: which of the overlays it still
/// owns is open (state mirrors the kind as <c>ShellState.Overlay</c>, #1020; this copy is read at the key), and whether the graph (not the detail list) has the toolkit's focus.
/// </summary>
/// <param name="Overlay">The open overlay of the overlay layer (help, palette, menu, history, output, confirm).</param>
/// <param name="IsGraphFocused">True when the graph list has the focus, false for the detail list.</param>
public readonly record struct InputFocus(OverlayKind Overlay, bool IsGraphFocused)
{
    /// <summary>True while any overlay of the overlay layer is open.</summary>
    public bool IsOverlayOpen => Overlay != OverlayKind.None;
}
