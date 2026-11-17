namespace Gitunia.Core.Lists;

/// <summary>How a list window follows its selection (ui-consistency decision 8).</summary>
public enum ScrollPolicy
{
    /// <summary>The window stays put while the selection is inside it and moves by the least amount at an edge (panes, editors).</summary>
    Edge,

    /// <summary>The selection is kept in the middle of the window where the list is long enough (overlays and popups).</summary>
    Centre,
}
