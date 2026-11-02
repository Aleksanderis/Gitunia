using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>Tells whether a tab is running an action, so its poller can wait for it.</summary>
public interface ITabBusySource
{
    /// <summary>The tab is running an operation right now.</summary>
    bool IsBusy(TabId tab);
}
