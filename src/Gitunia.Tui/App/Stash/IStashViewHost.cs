using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Stash;

/// <summary>What the stash view needs from the application: the active tab's session and the two redraws.</summary>
public interface IStashViewHost
{
    /// <summary>The session of the active tab, null when there is none.</summary>
    TerminalTabSession? ActiveSession();

    /// <summary>Puts the graph selection back in the detail pane.</summary>
    void ReselectGraphCommit();

    /// <summary>Rebuilds the detail pane.</summary>
    void RefreshDetail();
}
