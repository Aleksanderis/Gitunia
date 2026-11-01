using Gitunia.Tui.App;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Counts the quit requests.</summary>
public sealed class CountingQuit : IQuitRequests
{
    public int Requests { get; private set; }

    public int Forced { get; private set; }

    public void Request() => Requests++;

    public void Force() => Forced++;
}
