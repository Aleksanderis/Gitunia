using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>
/// Lets one operation of a kind run per tab at a time. Discarding and the editors share one gate; the commit actions keep
/// one each (migrated features, staging included, use the running flag in their state). Every gate comes from
/// <see cref="TabActivity"/>, which tab eviction asks before it cancels a tab's work.
/// </summary>
public sealed class TabOperationGate
{
    private readonly HashSet<TabId> _busy = [];

    /// <summary>An operation of some tab is running or its status re-read is still pending.</summary>
    public bool IsAnyBusy => _busy.Count > 0;

    public bool IsBusy(TabId tabId) => _busy.Contains(tabId);

    /// <summary>Begins the operation unless one already runs for the tab; false when it does.</summary>
    public bool Begin(TabId tabId) => _busy.Add(tabId);

    public void End(TabId tabId) => _busy.Remove(tabId);
}
