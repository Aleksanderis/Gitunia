using Gitunia.App;
using Gitunia.App.Tabs;

namespace Gitunia.Tui.App.Tabs;

/// <summary>The runtime session of the shown tab, from the registry the effect runner fills.</summary>
/// <param name="host">Holds the shown tab.</param>
/// <param name="registry">The sessions of the open tabs.</param>
public sealed class ActiveSessions(Dispatcher host, TabSessionRegistry<TerminalTabSession> registry) : IActiveSessions
{
    /// <inheritdoc />
    public TerminalTabSession? ActiveSession() =>
        host.Workspace.ActiveTabId is { } id ? registry.GetValueOrDefault(id) : null;
}
