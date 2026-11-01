using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Rebase;
using Gitunia.Core.Shell;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.App.Rebase;

/// <summary>
/// The view side of the interactive rebase's todo editor (ux-workflows.md, workflow 3, "Interactive rebase"): draws the
/// todo that lives in the shell state and turns keys, typed text and clicks into <see cref="RebaseEditorCommand"/>s. It
/// owns no state. The editor owns the keys while it is open and no dialog covers it; the Confirm opens on top of it, so
/// declining keeps the todo.
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="cover">Says whether another overlay covers the editor.</param>
/// <param name="glyphs">The characters the editor is drawn with.</param>
/// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
public sealed class RebaseEditorInput(
    Dispatcher host,
    IBranchSelectionSource selection,
    IOverlayCover cover,
    GraphGlyphSet glyphs,
    ChipKeys? keys = null) : IActionHandler, ITextKeyHandler
{
    private RebaseEditorSession Session => host.State.Shell.RebaseEditor;

    private ScreenExtent Screen => host.State.Shell.Screen ?? new ScreenExtent(0, 0);

    /// <summary>The open todo, null while the editor is closed.</summary>
    public RebaseEditorState? State => Session.Editor;

    public bool IsOpen => Session.IsOpen;

    /// <summary>True while the reword's message editor is open (the keymap is in Insert mode).</summary>
    public bool IsEditingMessage => Session.IsEditingMessage;

    /// <summary>The frame to draw, or null when closed or covered by a dialog.</summary>
    public OverlayFrame? CurrentFrame()
    {
        if (Session.Editor is not { } state || cover.IsOtherOverlayOpen())
        {
            return null;
        }

        var screen = Screen;

        return RebaseEditorFrameBuilder.Build(state, screen.Width, screen.Height, glyphs, keys);
    }

    /// <summary>
    /// Opens the editor on <see cref="ActionIds.RebaseInteractive"/> or <see cref="ActionIds.RebaseInteractiveRoot"/>
    /// and, while it is open and no dialog covers it, consumes every action (nothing behind it may move). Returns
    /// true when the action was consumed.
    /// </summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    public bool TryExecute(ActionId action, int? count)
    {
        if (Session.Editor is not { } state)
        {
            return TryOpen(action);
        }

        if (cover.IsOtherOverlayOpen())
        {
            return false;
        }

        var screen = Screen;
        var rows = RebaseEditorFrameBuilder.VisibleRows(state, screen.Width, screen.Height, keys);
        host.Dispatch(new RebaseEditorActionPressed(state.TabId, action, count, rows));

        return true;
    }

    /// <summary>
    /// A click on a frame line and column: a commit row is highlighted (handled, no view); a chip returns its mouse
    /// view for the caller to send through the mouse map. Returns null when the click hit no chip.
    /// </summary>
    /// <param name="frameLine">The frame line (0 is the title).</param>
    /// <param name="frameColumn">The column in the line.</param>
    /// <param name="handled">True when the editor took the click.</param>
    public string? OnLineClicked(int frameLine, int frameColumn, out bool handled)
    {
        handled = false;
        if (Session.Editor is not { Edit: null } state || cover.IsOtherOverlayOpen())
        {
            return null;
        }

        var screen = Screen;
        if (RebaseEditorFrameBuilder.EntryIndexAt(state, frameLine, screen.Width, screen.Height, keys) is { } index)
        {
            host.Dispatch(new RebaseEditorRowClicked(state.TabId, index));
            handled = true;

            return null;
        }

        var view = RebaseEditorFrameBuilder.ChipAt(state, frameLine, frameColumn, screen.Width, screen.Height, keys);
        handled = view is not null;

        return view;
    }

    /// <summary>Handles a key typed while a message is edited; false when no message is being edited.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (!Session.TakesText(host.Workspace.ActiveTabId) || Session.Editor is not { } state)
        {
            return false;
        }

        host.Dispatch(new RebaseEditorKeyTyped(state.TabId, key));

        return true;
    }

    private bool TryOpen(ActionId action)
    {
        var isRoot = action == ActionIds.RebaseInteractiveRoot;
        if (action != ActionIds.RebaseInteractive && !isRoot)
        {
            return false;
        }

        if (host.Workspace.ActiveTabId is not { } tabId || cover.IsOtherOverlayOpen())
        {
            return true;
        }

        var chosen = selection.Current();
        var picked = new RebaseSelection(chosen.RowKind, chosen.Name, chosen.CommitHash);
        host.Dispatch(new RebaseEditorOpenRequested(tabId, picked, isRoot));

        return true;
    }
}
