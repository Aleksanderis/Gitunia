using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Tests.Running;

/// <summary>Puts a tab into a state where an operation of a kind already runs, without starting it.</summary>
public static class AppReducerTestState
{
    public static AppState WithRunning(AppState state, TabId tabId, RunningOperationKind kind)
    {
        var tab = state.Workspace.FindTab(tabId)!;
        var running = new RunningOperations([new RunningOperation(1, kind)], 1);
        var tabs = state.Workspace.Tabs.Select(item => item.Id == tabId ? tab with { Running = running } : item);

        return state with { Workspace = state.Workspace with { Tabs = [.. tabs] } };
    }
}
