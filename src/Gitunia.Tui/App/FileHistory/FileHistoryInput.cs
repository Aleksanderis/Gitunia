using Gitunia.App;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Git.History.Models;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.FileHistory;

/// <summary>
/// The view side of the file history (ux-workflows.md, workflow 3, "File history"): draws the history that lives in
/// the shell state and turns keys and clicks into <see cref="FileHistoryCommand"/>s. It owns no state; while the
/// history is open it consumes every action, so nothing behind it moves.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="glyphs">The characters the history is drawn with.</param>
public sealed class FileHistoryInput(Dispatcher host, GraphGlyphSet glyphs) : IActionHandler
{
    private FileHistorySession Session => host.State.Shell.FileHistory;

    private ScreenExtent Screen => host.State.Shell.Screen ?? new ScreenExtent(0, 0);

    public bool IsOpen => Session.IsOpen;

    /// <summary>The lines the open history follows, null for a whole-file history or when none is open.</summary>
    public LineRange? Range => Session.History?.Range;

    /// <summary>The frame to draw, or null when the history is closed or the screen is too small for it.</summary>
    public OverlayFrame? CurrentFrame()
    {
        var screen = Screen;

        return Session.History is { } history
            ? FileHistoryFrameBuilder.Build(history, screen.Width, screen.Height, glyphs)
            : null;
    }

    /// <summary>While the history is open it consumes every action. Returns true when the action was consumed.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (Session.History is not { } history)
        {
            return false;
        }

        // A history whose tab was closed meanwhile closes and lets the action pass on.
        var tabIsOpen = host.Workspace.FindTab(history.TabId) is not null;
        var rows = FileHistoryFrameBuilder.VisibleRows(Screen.Height);
        host.Dispatch(new FileHistoryActionPressed(action, count, rows));

        return tabIsOpen;
    }

    /// <summary>
    /// A click on a frame line. A commit row is highlighted (a position is no action); the action row returns its
    /// mouse view, which the caller sends through the mouse map.
    /// </summary>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="handled">True when the history took the click.</param>
    public string? OnLineClicked(int frameLine, out bool handled)
    {
        handled = false;
        if (Session.History is not { } history)
        {
            return null;
        }

        var height = Screen.Height;
        if (FileHistoryFrameBuilder.EntryIndexAt(history, frameLine, height) is { } entry)
        {
            host.Dispatch(new FileHistoryEntryClicked(entry));
            handled = true;

            return null;
        }

        if (FileHistoryFrameBuilder.ActionAt(frameLine, height) is not { } view)
        {
            return null;
        }

        handled = true;

        return view;
    }
}
