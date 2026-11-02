using Gitunia.App;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Tabs;

namespace Gitunia.Tui.App.Selection;

/// <summary>What a cherry-pick, revert or rebase acts on: a focused sidebar's ref, the graph's Visual range or the selected commit.</summary>
/// <param name="host">Holds the focused pane.</param>
/// <param name="sessions">The shown tab's graph.</param>
/// <param name="visual">The Visual range.</param>
/// <param name="rows">The sidebar row or the selected commit.</param>
public sealed class PickSelectionSource(
    Dispatcher host,
    IActiveSessions sessions,
    VisualRangeInput visual,
    RowSelection rows) : IPickSelectionSource
{
    /// <inheritdoc />
    public PickSelection Current()
    {
        var chosen = rows.Current();
        if (chosen.RowKind is not null)
        {
            return new PickSelection(chosen.RowKind, chosen.CommitHash is { } hash ? [hash] : []);
        }

        var graph = sessions.ActiveSession()?.Graph;
        if (graph is null || visual is not { IsActive: true, Range: { } range }
            || host.Workspace.ActiveTab?.View.FocusedPane != PaneKind.Graph)
        {
            return new PickSelection(null, chosen.CommitHash is { } selected ? [selected] : []);
        }

        return new PickSelection(null, PickRange.Hashes(graph, range));
    }

    /// <inheritdoc />
    public void EndRange() => visual.End();
}
