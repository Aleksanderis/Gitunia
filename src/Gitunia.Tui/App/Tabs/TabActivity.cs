using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// Hands out every <see cref="TabOperationGate"/> of the app and knows whether any of them runs work for a tab, so a cold
/// tab is never evicted (its session and the token of its running git work cancelled) in the middle of an operation.
/// UI thread only, like the gates.
/// </summary>
public sealed class TabActivity
{
    private readonly List<TabOperationGate> _gates = [];

    /// <summary>A new gate whose operations count as tab activity.</summary>
    public TabOperationGate CreateGate()
    {
        var gate = new TabOperationGate();
        _gates.Add(gate);

        return gate;
    }

    /// <summary>Some gate runs an operation for the tab.</summary>
    public bool IsBusy(TabId tabId) => _gates.Exists(gate => gate.IsBusy(tabId));
}
