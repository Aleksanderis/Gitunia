namespace Gitunia.Tui.Search;

/// <summary>What the quick jump prompt needs from the shell: the list under the focus and a redraw of the status bar.</summary>
public interface IQuickJumpHost
{
    /// <summary>Creates the target for the focused pane, null when that pane has no quick jump or no tab is active.</summary>
    IJumpTarget? OpenTarget();

    /// <summary>Redraws the status bar (the lists redraw themselves through their target).</summary>
    void Redraw();
}
