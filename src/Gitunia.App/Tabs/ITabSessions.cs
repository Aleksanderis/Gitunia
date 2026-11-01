using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>The runtime sessions of the open tabs, for handlers that work on the stores a session holds.</summary>
public interface ITabSessions
{
    /// <summary>The session of an open tab, null when the tab is closed or has none.</summary>
    TabSession? Find(TabId tabId);
}
