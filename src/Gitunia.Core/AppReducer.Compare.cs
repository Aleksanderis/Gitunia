using System.Collections.Immutable;
using Gitunia.Core.Compare;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core;

// The comparison of two commits (#996). The mark and the bottom tab are the tab's own state, so the commands the
// feature reducer returns for the tab are applied here.
public static partial class AppReducer
{
    // A command for a tab that was closed meanwhile is dropped, like a late TabCommand.
    private static AppUpdate ReduceCompare(AppState state, RepositoryState tab, CompareCommand command)
    {
        var follow = state.Shell.Compare.Of(tab.Id);
        var context = new CompareContext(tab.Id, tab.View.CompareMark, tab.View.FocusedPane);
        var update = CompareReducer.Reduce(follow, command, context);
        var shell = state.Shell with { Compare = state.Shell.Compare.With(tab.Id, update.Follow) };

        return ApplyTabCommands(state with { Shell = shell }, tab.Id, update.TabCommands, update.Effects);
    }

    // The commands a feature reducer returns for the tab's own state go through the workspace reducer in order.
    private static AppUpdate ApplyTabCommands(
        AppState state,
        TabId tabId,
        ImmutableArray<RepositoryCommand> commands,
        ImmutableArray<AppEffect> effects)
    {
        var workspace = state.Workspace;
        var all = new List<AppEffect>(effects);
        foreach (var tabCommand in commands)
        {
            var applied = WorkspaceReducer.Reduce(workspace, new TabCommand(tabId, tabCommand));
            workspace = applied.Workspace;
            all.AddRange(applied.Effects);
        }

        return new AppUpdate(state with { Workspace = workspace }, [.. all]);
    }
}
