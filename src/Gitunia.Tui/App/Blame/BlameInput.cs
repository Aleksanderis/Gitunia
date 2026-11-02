using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;

namespace Gitunia.Tui.App.Blame;

/// <summary>
/// The view side of the blame screen (ux-workflows.md, "Blame"): draws the view that lives in the shell state, reads
/// the lines from the blame store and turns keys, typed text and clicks into <see cref="BlameCommand"/>s. It owns no
/// state; while the view is open it consumes every action, so nothing behind it moves.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="store">The lines and their commits.</param>
/// <param name="targets">Finds the file the open action stands for.</param>
/// <param name="glyphs">The characters the view is drawn with.</param>
/// <param name="clock">The current time for the ages in the gutter.</param>
public sealed class BlameInput(
    Dispatcher host,
    BlameStore store,
    IBlameTargets targets,
    GraphGlyphSet glyphs,
    TimeProvider? clock = null) : IActionHandler, ITextKeyHandler
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private BlameSession Session => host.State.Shell.Blame;

    private ScreenExtent Screen => host.State.Shell.Screen ?? new ScreenExtent(0, 0);

    public bool IsOpen => Session.IsOpen;

    /// <summary>The frame to draw, or null when the view is closed or the screen is too small for it.</summary>
    public OverlayFrame? CurrentFrame()
    {
        if (Session.View is not { } view)
        {
            return null;
        }

        var screen = Screen;

        return BlameFrameBuilder.Build(view, DataOf(Session), _clock.GetUtcNow(), screen.Width, screen.Height, glyphs);
    }

    /// <summary>Opens the view on <see cref="ActionIds.BlameOpen"/> and, while it is open, consumes every action.
    /// Returns true when the action was consumed.</summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (Session.View is not { } view)
        {
            return action == ActionIds.BlameOpen && Open();
        }

        // A view whose tab was closed meanwhile closes and lets the action pass on.
        var tabIsOpen = host.Workspace.FindTab(view.TabId) is not null;
        var rows = BlameFrameBuilder.VisibleRows(Screen.Height);
        var data = DataOf(Session);
        var owner = data?.OwnerOf(view.Cursor);
        host.Dispatch(new BlameActionPressed(
            action,
            count,
            rows,
            owner?.Id,
            owner?.PreviousPath ?? owner?.Path,
            data?.OriginalLineOf(view.Cursor) ?? 0,
            owner?.IsBoundary ?? false));

        return tabIsOpen;
    }

    /// <summary>Handles a key typed while the search box is open; false when it is not.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (!Session.TakesText(host.Workspace.ActiveTabId))
        {
            return false;
        }

        host.Dispatch(new BlameKeyTyped(key));

        return true;
    }

    /// <summary>
    /// A click on a frame line. A line is highlighted (a position is no action); the action row returns its mouse view,
    /// which the caller sends through the mouse map.
    /// </summary>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="frameColumn">The frame column.</param>
    /// <param name="handled">True when the view took the click.</param>
    public string? OnLineClicked(int frameLine, int frameColumn, out bool handled)
    {
        handled = false;
        if (Session.View is not { } view)
        {
            return null;
        }

        if (BlameFrameBuilder.LineIndexAt(view, frameLine, Screen.Height) is { } line)
        {
            host.Dispatch(new BlameRowClicked(line));
            handled = true;

            return null;
        }

        if (view.IsEditingSearch || BlameFrameBuilder.ActionAt(frameLine, frameColumn, Screen.Height) is not { } mouseView)
        {
            return null;
        }

        handled = true;

        return mouseView;
    }

    private bool Open()
    {
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var target = targets.Resolve(tab);
        if (target.Path is not { } path)
        {
            host.Dispatch(new BlameOpenRefused(target.Notice));
        }
        else
        {
            host.Dispatch(new BlameOpenRequested(path, target.Revision));
        }

        return true;
    }

    // Data of an older read (a view that was closed and opened again) is not shown.
    private BlameData? DataOf(BlameSession session) =>
        store.Current is { } data && data.Generation == session.Generation ? data : null;
}
