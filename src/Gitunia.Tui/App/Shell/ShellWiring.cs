using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.WorkingTree;
using Terminal.Gui.Input;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Subscribes the objects of the composition to each other's events: the state to the views, the widgets to the
/// controllers, the terminal's keys to the keymap. The order of the state subscriptions is the order they run in after
/// every command; <c>ShellWiringTests</c> pins it.
/// </summary>
/// <param name="shell">The composed shell.</param>
public sealed class ShellWiring(ShellComposition shell)
{
    /// <summary>Connects everything.</summary>
    public void Connect()
    {
        ConnectState();
        ConnectKeys();
        ConnectMouse();
        ConnectCursors();
        ConnectTerminal();
    }

    private void ConnectState()
    {
        var host = shell.Foundation.Host;
        var panes = shell.Panes;
        var screen = shell.Screen;
        host.Changed += panes.StashView.Reconcile;
        host.Changed += screen.Frames.RefreshOnStateChange;

        // These subscribe themselves to the host and the lists in their constructors; nothing else needs the instances.
        _ = new SidebarPresenter(host, shell.Foundation.Panes.Sidebar, shell.Foundation.Panes.Content.SidebarSource);
        _ = new WorkingTreePresenter(host, shell.Foundation.Panes, screen.View, shell.Foundation.WorkingFocus);
        _ = new GraphSelectionPresenter(host, shell.Foundation.Panes, screen.Binder, panes.StashView, panes.Detail);
        _ = new TabSwitchCoordinator(
            screen.Binder,
            new TabSwitchTargets(panes.Compare, panes.Visual, panes.DiffView, panes.DiffPane, panes.WorkingDiff),
            panes.Detail,
            shell.Foundation.Panes,
            shell.Overlays.Prompts);
        _ = new ScreenResizeCoordinator(
            host,
            shell.Foundation.Chrome,
            shell.Foundation.Terminal,
            new ResizeTargets(panes.DiffPane, shell.Overlays.Settings, shell.Keys.Overlays, screen.PopupTracker));

        host.Changed += screen.View.RefreshChrome;
        host.Changed += screen.Binder.Sync;
        host.Changed += screen.Focus.ApplyFocus;
        host.Changed += panes.Detail.Follow;
        host.Changed += screen.SizeDragger.OnStateChanged;
        host.Changed += panes.QuickJump.CloseIfStale;
        host.Changed += screen.Picker.OnStateChanged;
    }

    private void ConnectKeys()
    {
        var keys = shell.Keys.Keys;
        keys.PendingChanged += shell.Screen.View.RefreshChrome;
        keys.TextKeyPressed += shell.Screen.Picker.OnTextKey;
        keys.TextKeyPressed += shell.Panes.Sidebar.OnKey;
        shell.Route.Overlays.Changed += shell.Screen.Frames.Refresh;
    }

    private void ConnectMouse()
    {
        var view = shell.Screen.View;
        view.Gestured += shell.Keys.GestureRouter.OnGestured;
        view.MessageClicked += shell.Panes.Commits.PlaceCursor;
        view.MessageDragged += shell.Panes.Commits.DragSelection;
        view.EdgeDragged += shell.Screen.SizeDragger.OnEdgeDragged;
        view.OverlayLineClicked += shell.Keys.OverlayGestures.OnLineClicked;
        view.OverlayLineDoubleClicked += shell.Keys.OverlayGestures.OnLineDoubleClicked;
        view.OverlayLineTripleClicked += shell.Keys.OverlayGestures.OnLineTripleClicked;
        view.OverlayDragStarted += shell.Keys.OverlayGestures.OnDragStarted;
        view.OverlayDragged += shell.Keys.OverlayGestures.OnDragged;
        view.OverlayDragReleased += shell.Keys.OverlayGestures.OnDragReleased;
        view.PickerRowClicked += shell.Screen.Picker.OnRowClicked;
        view.ChromeRefreshed += shell.Screen.PopupTracker.Sync;
    }

    private void ConnectCursors()
    {
        var panes = shell.Foundation.Panes;
        var visual = shell.Panes.Visual;
        panes.Graph.ValueChanged += (_, _) => visual.OnCursorMoved();
        panes.Detail.ValueChanged += (_, _) => visual.OnCursorMoved();
        panes.Detail.ValueChanged += (_, _) => shell.Panes.DiffPane.OnDetailCursorMoved();
        panes.Diff.ValueChanged += (_, _) => visual.OnCursorMoved();
        panes.WorkingDiff.ValueChanged += (_, _) => visual.OnCursorMoved();
        panes.WorkingUnstaged.ValueChanged += (_, _) => visual.OnCursorMoved();
        panes.WorkingStaged.ValueChanged += (_, _) => visual.OnCursorMoved();
    }

    private void ConnectTerminal()
    {
        var terminal = shell.Foundation.Terminal;
        terminal.KeyDown += OnKeyDown;
        terminal.Pasted += shell.Keys.Input.OnPaste;
        terminal.FocusGained += shell.Effects.Runner.CheckActiveTabNow;
    }

    // Esc while a tab chip is dragged only puts the tab back; the keymap does not see it.
    private void OnKeyDown(object? sender, Key key)
    {
        if (key == Key.Esc && shell.Screen.View.CancelTabDrag())
        {
            key.Handled = true;

            return;
        }

        shell.Keys.Input.OnKeyDown(sender, key);
    }
}
