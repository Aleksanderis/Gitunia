using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>The shown tab's session, read from a test's own field on each ask.</summary>
/// <param name="session">Returns the session of the moment.</param>
public sealed class FakeActiveSessions(Func<TerminalTabSession?> session) : IActiveSessions
{
    public TerminalTabSession? ActiveSession() => session();
}
