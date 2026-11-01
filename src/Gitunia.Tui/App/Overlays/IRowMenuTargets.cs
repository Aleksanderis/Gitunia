namespace Gitunia.Tui.App.Overlays;

/// <summary>What the actions menu of the focused row offers.</summary>
public interface IRowMenuTargets
{
    /// <summary>
    /// Asked with the keymap view the menu opens from (the graph, the sidebar, a diff or a list), it returns the row's
    /// actions and where the row is on screen, or null when there is no row to act on.
    /// </summary>
    /// <param name="view">The keymap view.</param>
    RowMenuTarget? TargetOf(string view);
}
