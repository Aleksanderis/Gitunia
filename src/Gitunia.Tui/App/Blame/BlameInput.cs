using Gitunia.App;
using Gitunia.App.Blame;
using Gitunia.Core.Blame;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Shell;
using Gitunia.Core.Text;
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
    private BlamePoint? _dragAnchor;

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

        // The line history of a selection starts from its first line; every other key reads the cursor line.
        var isRange = action == ActionIds.LineHistoryOpen && view.Selection is not null;
        var range = view.Selection?.RangeTo(view.Cursor);
        var line = isRange && range is { } span ? span.First : view.Cursor;
        var owner = data?.OwnerOf(line);
        var lines = isRange && range is { } rangeLines ? rangeLines.Count : 1;
        host.Dispatch(new BlameActionPressed(
            action,
            count,
            rows,
            owner?.Id,
            owner?.PreviousPath ?? owner?.Path,
            data?.OriginalLineOf(line) ?? 0,
            owner?.IsBoundary ?? false,
            owner?.Path,
            TextOfLine(data, view.Cursor),
            lines,
            !isRange || IsOneCommit(data, line, lines)));

        return tabIsOpen;
    }

    /// <summary>A drag began at a frame line and column: from the code it selects characters, from the gutter whole
    /// lines. Does nothing outside the lines.</summary>
    /// <param name="frameLine">The frame line.</param>
    /// <param name="frameColumn">The frame column.</param>
    public void OnDragStarted(int frameLine, int frameColumn)
    {
        _dragAnchor = null;
        if (PointAt(frameLine, frameColumn, false) is not { } point)
        {
            return;
        }

        _dragAnchor = point;
        Select(point, point);
    }

    /// <summary>The pointer of a drag moved to a frame line and column; the moving end follows it.</summary>
    /// <param name="frameLine">The frame line.</param>
    /// <param name="frameColumn">The frame column.</param>
    public void OnDragged(int frameLine, int frameColumn)
    {
        if (_dragAnchor is { } anchor && PointAt(frameLine, frameColumn, true) is { } point)
        {
            Select(anchor, point);
        }
    }

    /// <summary>A double or triple click on the code selects the word or the text of the line under the pointer.</summary>
    /// <param name="frameLine">The frame line.</param>
    /// <param name="frameColumn">The frame column.</param>
    /// <param name="isWord">True for the word, false for the line.</param>
    /// <returns>True when the view took the click.</returns>
    public bool OnTextClicked(int frameLine, int frameColumn, bool isWord)
    {
        if (PointAt(frameLine, frameColumn, false) is not { IsCode: true } point)
        {
            return false;
        }

        var text = TextOfLine(DataOf(Session), point.Line);
        var span = isWord ? TextMotions.WordAt(text, point.Column) : new ColumnSpan(0, TextMotions.LineEnd(text));
        host.Dispatch(new BlameTextSelected(new TextPosition(point.Line, span.First), new TextPosition(point.Line, span.Last)));

        return true;
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

    private void Select(BlamePoint anchor, BlamePoint cursor)
    {
        if (anchor.IsCode)
        {
            host.Dispatch(new BlameTextSelected(
                new TextPosition(anchor.Line, anchor.Column),
                new TextPosition(cursor.Line, cursor.Column)));
        }
        else
        {
            host.Dispatch(new BlameLinesSelected(anchor.Line, cursor.Line));
        }
    }

    // The line and character under a frame position; a position outside the lines is moved onto the nearest one when
    // asked to (a drag past the first or last line).
    private BlamePoint? PointAt(int frameLine, int frameColumn, bool isNearest)
    {
        if (Session.View is not { IsEditingSearch: false } view || DataOf(Session) is not { } data)
        {
            return null;
        }

        var line = isNearest
            ? BlameFrameBuilder.NearestLineIndexAt(view, frameLine, Screen.Height)
            : BlameFrameBuilder.LineIndexAt(view, frameLine, Screen.Height);
        if (line is not { } found || found >= data.Count)
        {
            return null;
        }

        var column = BlameFrameBuilder.CharacterAt(data.TextOf(found), frameColumn, Screen.Width);

        return new BlamePoint(found, column ?? 0, column is not null);
    }

    private static string TextOfLine(BlameData? data, int line) =>
        data is not null && line >= 0 && line < data.Count ? data.TextOf(line) : string.Empty;

    // The lines come from one commit when every owner is the same known, committed commit and their original line
    // numbers follow each other.
    private static bool IsOneCommit(BlameData? data, int first, int count)
    {
        if (data?.OwnerOf(first) is not { } owner || owner.IsUncommitted)
        {
            return count == 1;
        }

        for (var line = first + 1; line < first + count; line++)
        {
            var other = data.OwnerOf(line);
            if (other?.Id != owner.Id || data.OriginalLineOf(line) != data.OriginalLineOf(line - 1) + 1)
            {
                return false;
            }
        }

        return true;
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
