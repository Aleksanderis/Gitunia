using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>Reads whether a tab runs an operation from the workspace state. UI thread only.</summary>
public sealed class WorkspaceBusySource(Dispatcher host) : ITabBusySource
{
    public bool IsBusy(TabId tab) => host.Workspace.FindTab(tab)?.Running.IsBusy == true;
}
