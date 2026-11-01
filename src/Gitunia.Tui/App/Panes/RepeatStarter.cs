using Gitunia.App;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// A step that took effect (the resize did not refuse, the context belongs to a diff) starts the sticky repeat mode.
/// </summary>
/// <param name="host">Holds the active tab.</param>
/// <param name="view">The window, whose diff pane tells whether the diff has options.</param>
/// <param name="repeat">The sticky repeat mode.</param>
/// <param name="paneSizes">Says whether the last resize step was refused.</param>
public sealed class RepeatStarter(Dispatcher host, MainView view, RepeatController repeat, PaneSizeInput paneSizes)
    : IRepeatStarter
{
    public void AfterPaneAction(ActionId action)
    {
        var isRefused = RepeatMode.IsResizeStep(action) && paneSizes.WasRefused;
        var isHistoryDiff = host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Diff;
        var hasNoDiffOptions = RepeatMode.IsContextStep(action)
            && isHistoryDiff
            && view.Diff.Source is DiffListSource { Document.IsFileContent: true };
        if (!isRefused && !hasNoDiffOptions)
        {
            repeat.Started(action);
        }
    }
}
