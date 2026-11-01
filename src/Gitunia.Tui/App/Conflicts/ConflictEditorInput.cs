using Gitunia.App;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.App.Conflicts;

/// <summary>
/// The view side of the conflict editor (ux-workflows.md, workflow 4, "Conflict editor"): draws the editor that lives
/// in the shell state and turns keys, typed text, clicks and drags into <see cref="ConflictCommand"/>s. It owns no
/// state but the drag in progress. While the editor is open it consumes every action, so nothing behind it moves; a
/// dialog or another overlay on top of it takes the keys instead (<c>isCovered</c>).
/// </summary>
/// <param name="host">The dispatcher that holds the state.</param>
/// <param name="glyphs">The characters the editor is drawn with.</param>
/// <param name="keys">Where the chips take their key text from; null uses the default keymap.</param>
public sealed class ConflictEditorInput(Dispatcher host, GraphGlyphSet glyphs, ChipKeys? keys = null)
{
    private const int StatusBarRows = 1;

    private bool _isDragging;

    private ConflictSession Session => host.State.Shell.Conflict;

    /// <summary>The open editor, null while it is closed.</summary>
    public ConflictEditorState? State => Session.Editor;

    public bool IsOpen => Session.IsOpen;

    /// <summary>True while the result is typed in (the keymap is in Insert mode).</summary>
    public bool IsEditing => State is { IsEditing: true };

    /// <summary>True while the cursor is in the result pane.</summary>
    public bool IsResultActive => State is { ActivePane: ConflictPane.Result };

    /// <summary>True while lines are selected in a version pane.</summary>
    public bool IsVisual => State is { IsVisual: true };

    /// <summary>
    /// The keymap view of the pane the cursor is in (the result's editor keys, or the versions' pick keys).
    /// </summary>
    public string ViewName => IsResultActive ? DefaultKeymap.ConflictResultView : DefaultKeymap.ConflictSideView;

    /// <summary>The frame to draw, or null when closed, covered by a dialog or the screen is too small.</summary>
    /// <param name="isCovered">True while an overlay other than this editor is open.</param>
    public OverlayFrame? CurrentFrame(bool isCovered)
    {
        if (State is not { } state || isCovered)
        {
            return null;
        }

        var size = EditorArea();

        return ConflictEditorFrameBuilder.Build(state, size.Width, size.Height, glyphs, keys);
    }

    /// <summary>
    /// Handles an action: opens the editor on <see cref="ActionIds.ConflictOpen"/> (any pane), or on <c>Enter</c>
    /// over a conflicted file of the Unstaged list; while it is open and no dialog covers it, consumes every action
    /// (nothing behind it may move). Returns true when the action was consumed.
    /// </summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, null when none was typed.</param>
    /// <param name="isCovered">True while an overlay other than this editor is open.</param>
    public bool TryExecute(ActionId action, int? count, bool isCovered)
    {
        if (State is not { } state)
        {
            return TryOpen(action, isCovered);
        }

        if (isCovered)
        {
            return false;
        }

        // An editor whose tab was closed meanwhile closes and lets the action pass on.
        var tabIsOpen = host.Workspace.FindTab(state.TabId) is not null;
        host.Dispatch(new ConflictActionPressed(action, count, Viewports()));

        return tabIsOpen;
    }

    /// <summary>Handles a key typed while the result is in Insert mode; false when it is not.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (State is not { IsEditing: true } state || host.Workspace.ActiveTabId != state.TabId)
        {
            return false;
        }

        host.Dispatch(new ConflictKeyTyped(key, Viewports()));

        return true;
    }

    /// <summary>
    /// A click on a frame line and column. A chip returns its mouse view for the caller to send through the mouse
    /// map; a header or a row moves the cursor there (the pane becomes the active one), and a double-click on a row of
    /// a version pane that lies in a conflict also takes that pane's version for the conflict, like a double-click on
    /// a change group in the diff stages it. Returns null when no chip was hit; <paramref name="handled"/> says
    /// whether anything was.
    /// </summary>
    /// <param name="isCovered">True while an overlay other than this editor is open.</param>
    /// <param name="frameLine">The frame line.</param>
    /// <param name="frameColumn">The frame column.</param>
    /// <param name="handled">True when the editor took the click.</param>
    /// <param name="isDoubleClick">True for the second click of a double-click.</param>
    public string? OnLineClicked(
        bool isCovered,
        int frameLine,
        int frameColumn,
        out bool handled,
        bool isDoubleClick = false)
    {
        handled = false;
        if (State is not { } state || isCovered)
        {
            return null;
        }

        var size = EditorArea();
        if (ConflictEditorFrameBuilder.TargetAt(state, frameLine, frameColumn, size.Width, size.Height, keys)
            is not { } target)
        {
            return null;
        }

        handled = true;
        if (target.Kind == ConflictTargetKind.Chip)
        {
            // The first click of a double-click already ran the chip: the second must not run it again.
            return isDoubleClick ? null : target.View;
        }

        host.Dispatch(new ConflictClicked(target, isDoubleClick, Viewports()));

        return null;
    }

    /// <summary>
    /// A drag began at a frame line and column. Over a row of a version pane the cursor goes there and the mouse view
    /// of the pane is returned for the caller to send the drag through the mouse map, which starts the Visual
    /// selection like <c>v</c> (taken by <c>&lt;space&gt;a</c> or the take chip). Null when the drag selects nothing.
    /// </summary>
    /// <param name="isCovered">True while an overlay other than this editor is open.</param>
    /// <param name="frameLine">The frame line.</param>
    /// <param name="frameColumn">The frame column.</param>
    public string? OnDragStarted(bool isCovered, int frameLine, int frameColumn)
    {
        _isDragging = false;
        if (State is not { IsEditing: false } state || isCovered)
        {
            return null;
        }

        var size = EditorArea();
        if (ConflictEditorFrameBuilder.TargetAt(state, frameLine, frameColumn, size.Width, size.Height, keys)
            is not { Kind: ConflictTargetKind.VersionRow } target)
        {
            return null;
        }

        host.Dispatch(new ConflictDragStarted(target.Pane, target.Index, Viewports()));
        _isDragging = true;

        return DefaultKeymap.ConflictSideView;
    }

    /// <summary>
    /// The pointer of a drag started by <see cref="OnDragStarted"/> is at a frame line: the selection reaches the row
    /// under it (the first or last shown row above or below the panes).
    /// </summary>
    /// <param name="isCovered">True while an overlay other than this editor is open.</param>
    /// <param name="frameLine">The frame line.</param>
    public void OnDragged(bool isCovered, int frameLine)
    {
        if (!_isDragging || State is not { IsVisual: true } state || isCovered)
        {
            return;
        }

        var size = EditorArea();
        if (ConflictEditorFrameBuilder.VersionRowAtLine(state, frameLine, size.Width, size.Height, keys) is { } row)
        {
            host.Dispatch(new ConflictDragged(row, Viewports()));
        }
    }

    private bool TryOpen(ActionId action, bool isCovered)
    {
        var isOpenKey = action == ActionIds.ConflictOpen;
        if (!isOpenKey && action != ActionIds.WorkingTreeOpenDiff)
        {
            return false;
        }

        if (host.Workspace.ActiveTab is not { } tab || isCovered)
        {
            return false;
        }

        // Enter keeps its meaning on every row but a conflicted file of the Unstaged list.
        if (!isOpenKey && ConflictedSelection.UnderCursor(tab) is null)
        {
            return false;
        }

        host.Dispatch(new ConflictOpenRequested(isOpenKey, Viewports()));

        return true;
    }

    // The whole screen but the status bar; the editor covers the tab row and the operation banner too.
    private ScreenExtent EditorArea()
    {
        var screen = host.State.Shell.Screen ?? new ScreenExtent(0, 0);

        return screen with { Height = Math.Max(0, screen.Height - StatusBarRows) };
    }

    // The pane rows at this screen size, which the reducer scrolls the panes with.
    private ConflictViewports? Viewports()
    {
        var size = EditorArea();

        return ConflictEditorFrameBuilder.ViewportsFor(size.Width, size.Height, keys);
    }
}
