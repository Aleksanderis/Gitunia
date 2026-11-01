using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The terminal side of the pane resize keys (<c>Ctrl-w &gt;</c>, <c>&lt;</c>, <c>+</c>, <c>-</c>, <c>=</c>): it reads the
/// arrangement of the panes the views keep (zoom, open diff) and hands it to the reducer with the key. Dragging a divider
/// with the mouse ends in the same sizes.
/// </summary>
/// <param name="host">Holds the active tab and takes the commands.</param>
/// <param name="diffPane">The diff pane, which knows the arrangement.</param>
/// <param name="workingDiff">The Changes view diff, which may fill the view.</param>
public sealed class PaneSizeInput(Dispatcher host, DiffPaneView diffPane, WorkingDiffController workingDiff)
{
    /// <summary>The last resize key changed nothing (a pane is zoomed, or the size is at its limit).</summary>
    public bool WasRefused { get; private set; }

    /// <summary>Runs the action when it is a resize key; false lets the next handler try.</summary>
    public bool TryExecute(ActionId action, int? count)
    {
        WasRefused = false;
        if (!IsResizeAction(action))
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is { } tabId)
        {
            var arrangement = diffPane.Arrangement();
            var layout = new PaneLayoutFacts(
                arrangement.IsDiffOpen,
                arrangement.Zoomed,
                arrangement.ZoomBoth,
                workingDiff.IsZoomed);
            var before = diffPane.SizesOfActiveTab();
            host.Dispatch(new PaneSizeCommand(tabId, action, count, layout));
            WasRefused = diffPane.SizesOfActiveTab() == before;
        }

        return true;
    }

    private static bool IsResizeAction(ActionId action) =>
        action == ActionIds.PaneWider
        || action == ActionIds.PaneNarrower
        || action == ActionIds.PaneTaller
        || action == ActionIds.PaneShorter
        || action == ActionIds.PaneResetSizes;
}
