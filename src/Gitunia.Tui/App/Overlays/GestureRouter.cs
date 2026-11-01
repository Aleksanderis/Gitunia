using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Input;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Overlays;

/// <summary>
/// Prepares a mouse gesture from a view before the mouse map decides on it: an open overlay takes the wheel and any
/// click (a click outside it closes it), a double-click right after such a click is ignored, and a drag or a click on a
/// row ends a Visual range.
/// </summary>
/// <param name="mouse">Runs the action a gesture is bound to.</param>
/// <param name="visual">The Visual range a gesture may end.</param>
/// <param name="services">What decides whether an overlay takes the gesture.</param>
public sealed class GestureRouter(MouseController mouse, VisualRangeInput visual, GestureServices services)
{
    /// <summary>Handles a gesture of a view.</summary>
    /// <param name="viewName">The keymap view (or the tab row, the picker, the overlay) the pointer was on.</param>
    /// <param name="gesture">What the user did.</param>
    /// <param name="target">The number of the clicked item, null for none.</param>
    public void OnGestured(string viewName, MouseGesture gesture, int? target)
    {
        if (gesture == MouseGesture.DoubleClick && services.Guard.IsGuarding)
        {
            return;
        }

        // The sidebar and the message box have no ranges; dragging there only moves the selection.
        if (gesture == MouseGesture.DragStart
            && viewName is DefaultKeymap.SidebarView or DefaultKeymap.WorkingMessageView)
        {
            return;
        }

        viewName = ViewAfterOverlay(viewName, gesture);

        // A drag starts a fresh range; a click on a row ends the range (the click moved the cursor, a position).
        if (gesture == MouseGesture.DragStart || (gesture is MouseGesture.Click or MouseGesture.DoubleClick && IsRangePane(viewName)))
        {
            visual.End();
        }

        // The dirty marker belongs to a tab chip: the click first switches to that tab, then opens its working tree (#331).
        if (viewName == DefaultMouseMap.TabDirtyMarker)
        {
            mouse.Handle(DefaultMouseMap.TabsView, gesture, target);
        }

        mouse.Handle(viewName, gesture, target);

        // A key redraws the chrome through the pending-keys event; a mouse action has no such path (titles, status bar, mode).
        services.Panes.Chrome.RefreshChrome();
    }

    // While an overlay is open the wheel scrolls it wherever the pointer is (other gestures on the panes behind it are
    // ignored) and a click anywhere outside it closes it, like Esc.
    private string ViewAfterOverlay(string viewName, MouseGesture gesture)
    {
        if (!services.Cover.IsAnyOverlayOpen())
        {
            return viewName;
        }

        if (gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            if (viewName == MainView.OverlayGestureView)
            {
                services.Editors.Settings.FocusColumnAt(services.Panes.Overlay.PointerColumn);
            }

            return services.Cover.ViewName();
        }

        if (gesture == MouseGesture.Click)
        {
            services.Guard.Mark();

            return DefaultMouseMap.BackdropView;
        }

        return viewName;
    }

    private static bool IsRangePane(string viewName) =>
        viewName is DefaultKeymap.GraphView
            or DefaultKeymap.DetailView
            or DefaultKeymap.DetailChangesView
            or DefaultKeymap.DetailFilesView
            or DefaultKeymap.DiffView
            or DefaultKeymap.WorkingUnstagedView
            or DefaultKeymap.WorkingStagedView
            or DefaultKeymap.WorkingUnstagedDiffView
            or DefaultKeymap.WorkingStagedDiffView;
}
