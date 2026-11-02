using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Panes;

/// <summary>The history diff pane's diff of the active tab.</summary>
/// <param name="sessions">The active tab's runtime session.</param>
public sealed class ActiveHistoryDiff(IActiveSessions sessions) : IActiveDiff
{
    public IDiffViewState? Current() => sessions.ActiveSession()?.Diff;
}
