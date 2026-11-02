using Gitunia.Core.Workspaces.Repositories;
using Gitunia.App;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Input;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The pane sizes are repository tab state: whoever changed them (the resize keys, a divider drag), the panes follow. A
/// drag of a divider or a pane title row becomes a command.
/// </summary>
public sealed class PaneSizeDragger
{
    private readonly Dispatcher _host;
    private readonly PaneArrangements _arrangements;
    private readonly IPaneChrome _chrome;
    private readonly IActiveSessions _sessions;
    private readonly KeyFocus _focus;
    private PaneSizes? _laidOutSizes;

    /// <param name="host">Takes the size command.</param>
    /// <param name="arrangements">The saved sizes and the zoom.</param>
    /// <param name="chrome">The screen height; lays the panes out again.</param>
    /// <param name="sessions">A tab without a diff has nothing to resize.</param>
    /// <param name="focus">A drag is ignored while an overlay or the picker is open.</param>
    public PaneSizeDragger(
        Dispatcher host,
        PaneArrangements arrangements,
        IPaneChrome chrome,
        IActiveSessions sessions,
        KeyFocus focus)
    {
        _host = host;
        _arrangements = arrangements;
        _chrome = chrome;
        _sessions = sessions;
        _focus = focus;
    }

    /// <summary>A divider or a pane title row is dragged to a screen cell.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="column">The pointer column.</param>
    /// <param name="row">The pointer row.</param>
    public void OnEdgeDragged(PaneEdge edge, int column, int row)
    {
        if (_focus.IsBlocked || _sessions.ActiveSession()?.Diff is null || _arrangements.Arrangement().Zoomed is not null)
        {
            return;
        }

        var sizes = PaneResizer.Drag(_arrangements.SizesOfActiveTab(), edge, column, row, _chrome.ScreenSize.Height);
        if (_host.Workspace.ActiveTabId is { } tabId && _arrangements.SizesOfActiveTab() != sizes)
        {
            _host.Dispatch(new TabCommand(tabId, new SetPaneSizesCommand(sizes.SidebarWidth, sizes.BottomHeight, sizes.DetailWidth)));
        }
    }

    /// <summary>Lays the panes out again when the active tab's sizes changed.</summary>
    public void OnStateChanged()
    {
        var sizes = _arrangements.SizesOfActiveTab();
        if (sizes != _laidOutSizes)
        {
            _laidOutSizes = sizes;
            _chrome.Relayout();
        }
    }
}
