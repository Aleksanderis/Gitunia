using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Panes;

/// <summary>The Changes view diff of the active tab.</summary>
/// <param name="sessions">The active tab's runtime session.</param>
public sealed class ActiveWorkingDiff(IActiveSessions sessions) : IActiveDiff
{
    public IDiffViewState? Current() => sessions.ActiveSession()?.WorkingDiff;
}
