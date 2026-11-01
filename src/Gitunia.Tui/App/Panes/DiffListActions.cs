using System.Globalization;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Shell;
using Gitunia.App;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git.Diff.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The part of the diff pane that does not depend on where the diff comes from: cursor and viewport handling,
/// scrolling, wrap, side by side, hunk jumps, search and copy. The history diff pane and the Changes view diff box
/// both run on one instance each and keep to themselves what loads the diff and what leaving it means. UI thread only.
/// </summary>
public sealed class DiffListActions
{
    private readonly ListView _diffList;
    private readonly Dispatcher _host;
    private readonly ClipboardCopier _clipboard;
    private readonly Func<IDiffViewState?> _active;
    private readonly string _sideBySideHint;
    private readonly string _anchorName;
    private bool _isSyncing;

    /// <param name="diffList">The list of the diff pane.</param>
    /// <param name="host">Switches the input mode (Insert while the search box is open).</param>
    /// <param name="clipboard">Copies diff text.</param>
    /// <param name="active">The active tab's diff, null while it has none.</param>
    /// <param name="sideBySideHint">Appended to the notice when side by side is on but the pane is too narrow.</param>
    /// <param name="anchorName">What the next and previous anchors are called in notices: "hunk", or "change" in the Changes view diff.</param>
    public DiffListActions(
        ListView diffList,
        Dispatcher host,
        ClipboardCopier clipboard,
        Func<IDiffViewState?> active,
        string sideBySideHint,
        string anchorName = "hunk")
    {
        _diffList = diffList;
        _host = host;
        _clipboard = clipboard;
        _active = active;
        _sideBySideHint = sideBySideHint;
        _anchorName = anchorName;
        diffList.ValueChanged += (_, _) => RememberCursor();
        diffList.ViewportChanged += (_, _) => SyncWidth();
    }

    /// <summary>The remark the last action left for the status bar.</summary>
    public string Notice { get; set; } = string.Empty;

    /// <summary>True while the list is being moved by code, so the cursor is not remembered.</summary>
    public bool IsRestoring { get; set; }

    /// <summary>Reads the options a key changes, false when the key is not an option key.</summary>
    public static bool TryResolveOptions(DiffOptions options, ActionId action, out DiffOptions changed)
    {
        if (action == ActionIds.DiffMoreContext)
        {
            changed = options.MoreContext();
        }
        else if (action == ActionIds.DiffLessContext)
        {
            changed = options.LessContext();
        }
        else if (action == ActionIds.DiffToggleWholeFile)
        {
            changed = options with { WholeFile = !options.WholeFile };
        }
        else if (action == ActionIds.DiffToggleIgnoreWhitespace)
        {
            changed = options with { IgnoreWhitespace = !options.IgnoreWhitespace };
        }
        else
        {
            changed = options;
            return false;
        }

        return true;
    }

    /// <summary>The quick-jump target of a diff shown in this list: the rows in view are labelled, a label moves the cursor to its row.</summary>
    /// <param name="pane">The pane the diff is.</param>
    /// <param name="diff">The diff to jump in.</param>
    public IJumpTarget JumpTarget(PaneKind pane, IDiffViewState diff) => new ListJumpTarget(
        pane,
        text => diff.Source.JumpLabels(_diffList, text),
        text => ShowJumpLabels(diff, text),
        row => Select(diff, row, atTop: false));

    public string StatusLine(IDiffViewState? diff) =>
        diff is { Search.IsOpen: true } ? diff.Search.Describe(SearchRow(diff)) : string.Empty;

    public DiffPaneModel ModelOf(IDiffViewState diff, DiffOptions options, bool isFocused) => new(
        diff.Source.Document,
        CursorRow(diff),
        diff.Source.RowOf(_diffList.Viewport.Y),
        isFocused,
        diff.Search.Highlight,
        diff.Source.IsWrapped,
        DiffOptionsLabel.Of(
            options,
            diff.Source.IsSideBySide,
            diff.Source.ShowsSideBySide,
            diff.Source.ShowsWhitespace),
        ShowWhitespace: diff.Source.ShowsWhitespace);

    /// <summary>
    /// Runs the keys every diff shares: moving, wrap, side by side, copy, hunk jumps and opening or stepping a search.
    /// </summary>
    public bool TryExecuteView(IDiffViewState diff, ActionId action, int? count)
    {
        var document = diff.Source.Document;
        var row = CursorRow(diff);
        var cursor = _diffList.SelectedItem ?? 0;
        if (ListMotion.TryMove(action, count, cursor, diff.Source.Count, _diffList.Viewport.Height, out var target))
        {
            SelectLine(target);
        }
        else if (action == ActionIds.DiffToggleWrap)
        {
            diff.Source.SetWrapped(!diff.Source.IsWrapped);
            Select(diff, row, atTop: true);
            Notice = diff.Source.IsWrapped ? "Word wrap on" : "Word wrap off";
        }
        else if (action == ActionIds.DiffToggleWhitespace)
        {
            diff.Source.SetWhitespace(!diff.Source.ShowsWhitespace);
            Notice = diff.Source.ShowsWhitespace ? "Whitespace characters shown" : "Whitespace characters hidden";
        }
        else if (action == ActionIds.DiffToggleSideBySide)
        {
            ToggleSideBySide(diff, row);
        }
        else if (action == ActionIds.DiffCopyLine || action == ActionIds.DiffCopyFile)
        {
            Copy(document, row, action == ActionIds.DiffCopyFile);
        }
        else if ((action == ActionIds.DiffNextHunk || action == ActionIds.DiffPreviousHunk) && document.IsFileContent)
        {
            Notice = "The file content has no changes to jump to";
        }
        else if (action == ActionIds.DiffNextHunk || action == ActionIds.DiffPreviousHunk)
        {
            Jump(diff, document.HunkRows, row, action == ActionIds.DiffNextHunk, count, _anchorName);
        }
        else if (action == ActionIds.DiffSearchOpen)
        {
            ApplySearch(diff, DiffSearch.Open());
            _host.SetKeyMode(KeyMode.Insert);
        }
        else if (action == ActionIds.DiffSearchNext || action == ActionIds.DiffSearchPrevious)
        {
            var update = DiffSearch.Step(diff.Search, action == ActionIds.DiffSearchNext, count ?? 1, SearchRow(diff));
            ApplySearch(diff, update);
            Notice = update.State.Notice;
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>Moves the diff's cursor for a scroll key pressed in the file list beside it; false for any other action.</summary>
    public bool TryScroll(IDiffViewState diff, ActionId action, int? count)
    {
        var motion = MotionOf(action);
        if (motion is null || !ListMotion.TryMove(motion.Value, count, _diffList.SelectedItem ?? 0, diff.Source.Count, _diffList.Viewport.Height, out var target))
        {
            return false;
        }

        SelectLine(target);
        return true;
    }

    /// <summary>Runs the keys of the search box (accept or cancel), false for any other key.</summary>
    public bool ExecuteInSearchBox(IDiffViewState diff, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            var update = DiffSearch.Accept(diff.Search, diff.Source.Document, SearchRow(diff));
            ApplySearch(diff, update);
            Notice = update.State.Notice;
        }
        else if (action == ActionIds.Cancel)
        {
            ApplySearch(diff, DiffSearch.Cancel());
        }
        else
        {
            return false;
        }

        _host.SetKeyMode(KeyMode.Normal);
        return true;
    }

    /// <summary>
    /// Edits the search text with a typed key; <paramref name="document"/> is what the search runs over.
    /// </summary>
    public bool HandleTextKey(IDiffViewState? diff, DiffDocument document, KeyEvent key)
    {
        if (diff is not { Search.Phase: DiffSearchPhase.Editing })
        {
            return false;
        }

        if (PromptTextEditor.Apply(diff.Search.Text, key) is { } text && text != diff.Search.Text)
        {
            ApplySearch(diff, DiffSearch.Edit(diff.Search, text, document));
        }

        return true;
    }

    /// <summary>Drops a search that is still being typed (not an accepted one) and leaves Insert mode.</summary>
    public void AbandonTypedSearch(IDiffViewState diff)
    {
        if (diff.Search.Phase != DiffSearchPhase.Editing)
        {
            return;
        }

        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
        _host.SetKeyMode(KeyMode.Normal);
        _diffList.SetNeedsDraw();
    }

    /// <summary>
    /// Ends the search of a diff that was replaced by another one, which the old search no longer describes.
    /// </summary>
    public void ForgetSearch(IDiffViewState diff)
    {
        if (diff.Search.Phase == DiffSearchPhase.Editing)
        {
            _host.SetKeyMode(KeyMode.Normal);
        }

        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
    }

    public void ApplySearch(IDiffViewState diff, DiffSearchUpdate update)
    {
        diff.Search = update.State;
        diff.Source.Highlight = update.State.Highlight;
        diff.SearchRow = null;
        if (update.RevealRow is { } row)
        {
            Select(diff, row, atTop: false);
            diff.SearchRow = row;
        }

        _diffList.SetNeedsDraw();
    }

    public void Jump(IDiffViewState diff, IReadOnlyList<int> anchors, int row, bool forward, int? count, string what)
    {
        if (DiffNavigator.Step(anchors, row, forward, count ?? 1) is { } target)
        {
            Select(diff, target, atTop: true);
        }
        else
        {
            Notice = forward ? $"No next {what}" : $"No previous {what}";
        }
    }

    // Side by side, the cursor row is the first row of a line, but a match may sit on the line's other row:
    // stepping starts from the matched row while the cursor is still on its line.
    public int SearchRow(IDiffViewState diff)
    {
        if (diff.SearchRow is { } matched && diff.Source.LineOf(matched) == (_diffList.SelectedItem ?? 0))
        {
            return matched;
        }

        return CursorRow(diff);
    }

    public int CursorRow(IDiffViewState diff) => diff.Source.RowOf(_diffList.SelectedItem ?? 0);

    public int ListTextWidth() => PanePadding.TextWidth(_diffList.Viewport.Width, ScreenPadding.Columns(_diffList));

    public void Select(IDiffViewState diff, int row, bool atTop)
    {
        var count = _diffList.Source?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(diff.Source.LineOf(row), 0, count - 1);
        _diffList.SelectedItem = clamped;
        if (atTop)
        {
            _diffList.Viewport = _diffList.Viewport with
            {
                Y = Math.Min(clamped, Math.Max(0, count - _diffList.Viewport.Height)),
            };
        }
        else
        {
            _diffList.EnsureSelectedItemVisible();
        }
    }

    /// <summary>Puts the list back on the cursor and top row a tab remembered.</summary>
    public void RestoreRemembered(IDiffViewState diff)
    {
        diff.Source.Width = ListTextWidth();
        if (diff.Source.Count > 0)
        {
            _diffList.SelectedItem = Math.Min(diff.Source.LineOf(diff.SelectedRow), diff.Source.Count - 1);
            _diffList.Viewport = _diffList.Viewport with { Y = diff.Source.LineOf(diff.TopRow) };
        }
    }

    /// <summary>
    /// Gives the diff source the list's width; when wrapping is on and the width changed, the cursor and the top row
    /// are kept.
    /// </summary>
    public void SyncWidth()
    {
        if (_isSyncing || _active() is not { } diff || diff.Source.Width == ListTextWidth())
        {
            return;
        }

        _isSyncing = true;
        try
        {
            if (!diff.Source.IsWrapped && !diff.Source.IsSideBySide)
            {
                diff.Source.Width = ListTextWidth();
                return;
            }

            var row = CursorRow(diff);
            var top = diff.Source.RowOf(_diffList.Viewport.Y);
            diff.Source.Width = ListTextWidth();
            IsRestoring = true;
            try
            {
                Select(diff, row, atTop: false);
                _diffList.Viewport = _diffList.Viewport with
                {
                    Y = Math.Min(diff.Source.LineOf(top), Math.Max(0, diff.Source.Count - _diffList.Viewport.Height)),
                };
            }
            finally
            {
                IsRestoring = false;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void RememberCursor()
    {
        if (!IsRestoring && _active() is { } diff)
        {
            diff.SelectedRow = CursorRow(diff);
            diff.TopRow = diff.Source.RowOf(_diffList.Viewport.Y);
        }
    }

    private void ShowJumpLabels(IDiffViewState diff, string? text)
    {
        diff.Source.JumpText = text;
        _diffList.SetNeedsDraw();
    }

    private void SelectLine(int line)
    {
        var count = _diffList.Source?.Count ?? 0;
        if (count > 0)
        {
            _diffList.SelectedItem = Math.Clamp(line, 0, count - 1);
            _diffList.EnsureSelectedItemVisible();
        }
    }

    private void Copy(DiffDocument document, int row, bool wholeFile)
    {
        if (row < 0
            || row >= document.Rows.Count
            || (!wholeFile && document.Rows[row].Kind is DiffRowKind.Note or DiffRowKind.Warning)
            || (document.IsFileContent && document.Rows[row].Kind == DiffRowKind.Note))
        {
            Notice = "Nothing to copy";
        }
        else if (document.IsFileContent)
        {
            _clipboard.Copy(
                wholeFile ? FileContentDocument.Text(document) : document.Rows[row].Text,
                wholeFile ? "file content" : "line");
        }
        else if (!wholeFile)
        {
            _clipboard.Copy(DiffTextWriter.Line(document.Rows[row]), "diff line");
        }
        else if (document.Rows[row].FileIndex < 0)
        {
            Notice = "The cursor is not in a file";
        }
        else
        {
            _clipboard.Copy(DiffTextWriter.File(document, document.Rows[row].FileIndex), "file diff");
        }
    }

    private void ToggleSideBySide(IDiffViewState diff, int row)
    {
        diff.Source.SetSideBySide(!diff.Source.IsSideBySide);
        Select(diff, row, atTop: true);
        if (!diff.Source.IsSideBySide)
        {
            Notice = "Side by side off";
        }
        else
        {
            var needed = DiffSplitRenderer.MinimumWidth.ToString(CultureInfo.InvariantCulture);
            Notice = $"Side by side on: needs {needed} columns{_sideBySideHint}";
            if (diff.Source.ShowsSideBySide)
            {
                Notice = "Side by side on";
            }
        }
    }

    private static ActionId? MotionOf(ActionId action)
    {
        if (action == ActionIds.DiffScrollDown)
        {
            return ActionIds.ListDown;
        }

        if (action == ActionIds.DiffScrollUp)
        {
            return ActionIds.ListUp;
        }

        if (action == ActionIds.DiffScrollHalfPageDown)
        {
            return ActionIds.ListHalfPageDown;
        }

        return action == ActionIds.DiffScrollHalfPageUp ? ActionIds.ListHalfPageUp : null;
    }
}
