using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Remote;

/// <summary>Tells whether a fetch, pull or push runs on a tab (until that state moves into the tab state).</summary>
public interface IRemoteOperationSource
{
    /// <summary>Whether a fetch, pull or push of the tab is running.</summary>
    bool IsRunning(TabId tabId);
}
