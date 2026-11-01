namespace Gitunia.Tui.App.Tabs;

/// <summary>Gives the runtime session of the shown tab to the views that draw its stores.</summary>
public interface IActiveSessions
{
    /// <summary>The shown tab's session, null when there is no tab or it has none yet.</summary>
    TerminalTabSession? ActiveSession();
}
