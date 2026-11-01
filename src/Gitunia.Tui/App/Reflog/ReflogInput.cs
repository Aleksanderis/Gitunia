using Gitunia.Tui.Input;
using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Reflog;
using Gitunia.Core.Shell;
using Gitunia.Core.Sidebar;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Reflog;

/// <summary>
/// The view side of the reflog list (ux-workflows.md, workflow 3, "Reflog"): draws the list that lives in the shell
/// state and turns keys, typed text and clicks into <see cref="ReflogCommand"/>s. It owns no state; while the list is
/// open it consumes every action, so nothing behind it moves.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="selection">What the sidebar cursor is on, to pick the branch whose reflog opens.</param>
/// <param name="glyphs">The characters the list is drawn with.</param>
public sealed class ReflogInput(Dispatcher host, IBranchSelectionSource selection, GraphGlyphSet glyphs) : IActionHandler, ITextKeyHandler
{
    private ReflogSession Session => host.State.Shell.Reflog;

    private ScreenExtent Screen => host.State.Shell.Screen ?? new ScreenExtent(0, 0);

    public bool IsOpen => Session.IsOpen;

    /// <summary>The frame to draw, or null when the list is closed or the screen is too small for it.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var screen = Screen;

        return Session.List is { } list ? ReflogFrameBuilder.Build(list, screen.Width, screen.Height, glyphs) : null;
    }

    /// <summary>Opens the list on <see cref="ActionIds.ReflogOpen"/> and, while it is open, consumes every action.
    /// Returns true when the action was consumed.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (Session.List is not { } list)
        {
            if (action != ActionIds.ReflogOpen)
            {
                return false;
            }

            host.Dispatch(new ReflogOpenRequested(OpenedBranch()));

            return true;
        }

        // A list whose tab was closed meanwhile closes and lets the action pass on.
        var tabIsOpen = host.Workspace.FindTab(list.TabId) is not null;
        var rows = ReflogFrameBuilder.VisibleRows(list, Screen.Height);
        host.Dispatch(new ReflogActionPressed(action, count, rows));

        return tabIsOpen;
    }

    /// <summary>Handles a key typed while the search box or the ref picker is open; false when neither is.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (!Session.TakesText(host.Workspace.ActiveTabId))
        {
            return false;
        }

        host.Dispatch(new ReflogKeyTyped(key));

        return true;
    }

    /// <summary>
    /// A click on a frame line. An entry or a ref picker row is highlighted (a position is no action); an action row
    /// returns its mouse view, which the caller sends through the mouse map.
    /// </summary>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="handled">True when the list took the click.</param>
    public string? OnLineClicked(int frameLine, out bool handled)
    {
        handled = false;
        if (Session.List is not { } list)
        {
            return null;
        }

        var height = Screen.Height;
        if (list.Picker is null && ReflogFrameBuilder.EntryIndexAt(list, frameLine, height) is { } entry)
        {
            host.Dispatch(new ReflogEntryClicked(entry));
            handled = true;

            return null;
        }

        if (list.Picker is { } picker)
        {
            if (ReflogFrameBuilder.PickerIndexAt(picker, frameLine, height) is not { } index)
            {
                return null;
            }

            host.Dispatch(new ReflogPickerRowClicked(index));
            handled = true;

            return DefaultMouseMap.ReflogPickerRow;
        }

        if (list.IsEditingSearch || ReflogFrameBuilder.ActionAt(list, frameLine, height) is not { } view)
        {
            return null;
        }

        handled = true;

        return view;
    }

    private string? OpenedBranch()
    {
        var chosen = selection.Current();
        var isBranch = chosen.RowKind == SidebarRowKind.LocalBranch && chosen.Name is not null;

        return isBranch ? chosen.Name : null;
    }
}
