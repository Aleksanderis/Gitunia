using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App;

/// <summary>Finds the runtime session of an open tab, for the effect handlers that work on its stores.</summary>
public interface ITabSessions
{
    /// <summary>The session of an open tab, null when the tab has none.</summary>
    /// <param name="tabId">The tab.</param>
    TabSession? FindSession(TabId tabId);
}
