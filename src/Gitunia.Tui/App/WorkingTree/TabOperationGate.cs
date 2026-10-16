using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Lets one working-tree operation (a stage, an unstage, a discard) run per tab at a time. Staging and discarding share one gate,
/// so a discard never starts while a stage of the same tab is still running or its status is still being re-read.
/// </summary>
public sealed class TabOperationGate
{
    private readonly HashSet<TabId> _busy = [];

    /// <summary>An operation of some tab is running or its status re-read is still pending.</summary>
    public bool IsAnyBusy => _busy.Count > 0;

    public bool IsBusy(TabId tabId) => _busy.Contains(tabId);

    public void Begin(TabId tabId) => _busy.Add(tabId);

    public void End(TabId tabId) => _busy.Remove(tabId);
}
