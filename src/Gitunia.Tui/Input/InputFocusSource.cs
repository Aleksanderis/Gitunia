using Gitunia.Core.Input;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.Input;

/// <summary>What the toolkit and the overlay layer know about the focus: the open overlay and whether the detail list has it.</summary>
/// <param name="overlays">The overlay state.</param>
/// <param name="panes">The detail list, whose toolkit focus tells the graph from the detail pane.</param>
public sealed class InputFocusSource(OverlayStore overlays, PaneViews panes) : IInputFocusSource
{
    /// <inheritdoc />
    public InputFocus Current() => new(overlays.State.Kind, !panes.Detail.HasFocus);
}
