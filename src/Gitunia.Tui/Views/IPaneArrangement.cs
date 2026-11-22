using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.Views;

/// <summary>Which panes are shown and how big, for the active tab.</summary>
public interface IPaneArrangement
{
    /// <summary>The arrangement for the active tab; a zoom that does not match the focused pane is not part of it.</summary>
    PaneArrangement Arrangement();

    /// <summary>An operation is in progress in the active tab, so its banner has a row of its own below the frame.</summary>
    bool HasBanner();
}
