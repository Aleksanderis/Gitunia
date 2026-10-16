using System.Globalization;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Diff.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Runs the diff box of the working-tree view of the active tab. It follows the file under the cursor of the focused list (the
/// working-tree diff for a file in Unstaged, the index diff for one in Staged), loads it off the UI thread through the tab's
/// <see cref="WorkingDiffStore"/> and shows it in the same widget as the history diff: scrolling, hunk jumps, wrap, side by side,
/// context, search and copy. It is also the diff's search prompt, so it plugs into the same prompt list as the graph search.
/// Everything it changes about focus goes through workspace commands.
/// </summary>
public sealed class WorkingDiffController : IPromptHandler
{
    private readonly WorkspaceHost _host;
    private readonly Func<TabSession?> _activeSession;
    private readonly ListView _diffList;
    private readonly Action<KeyMode> _setMode;
    private readonly Action _refreshChrome;
    private readonly ClipboardCopier _clipboard;
    private TabSession? _hooked;
    private bool _isRestoring;
    private bool _isSyncing;

    /// <param name="host">The workspace the focus commands go to.</param>
    /// <param name="activeSession">The active tab's runtime session.</param>
    /// <param name="diffList">The list of the diff box.</param>
    /// <param name="setMode">Switches the input mode (Insert while the search box is open).</param>
    /// <param name="refreshChrome">Redraws the frame titles and the status bar after the diff changed on its own (it finished loading).</param>
    /// <param name="clipboard">Copies diff text.</param>
    public WorkingDiffController(
        WorkspaceHost host,
        Func<TabSession?> activeSession,
        ListView diffList,
        Action<KeyMode> setMode,
        Action refreshChrome,
        ClipboardCopier clipboard)
    {
        _host = host;
        _activeSession = activeSession;
        _diffList = diffList;
        _setMode = setMode;
        _refreshChrome = refreshChrome;
        _clipboard = clipboard;
        diffList.ValueChanged += (_, _) => RememberCursor();
        diffList.ViewportChanged += (_, _) => SyncWidth();
        host.Changed += OnWorkspaceChanged;
    }

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine =>
        _activeSession()?.WorkingDiff is { Search.IsOpen: true } diff ? diff.Search.Describe(SearchRow(diff)) : string.Empty;

    /// <summary>The title of the diff box: the side, the hunk the cursor is in and the file, plus the markers of the diff options.</summary>
    public string TitleText()
    {
        var tab = _host.Workspace.ActiveTab;
        var model = _activeSession()?.WorkingDiff is { } diff
            ? ModelOf(diff, WorkingPanes.IsDiff(tab?.View.FocusedPane ?? PaneKind.Graph))
            : null;
        return WorkingTreePaneRenderer.DiffTitle(tab?.WorkingTree, model);
    }

    /// <summary>The diff box as the frame composer draws it, null while the tab has no diff.</summary>
    public DiffPaneModel? Model() =>
        _activeSession()?.WorkingDiff is { } diff
            ? ModelOf(diff, WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
            : null;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_activeSession()?.WorkingDiff is not { } diff
            || _host.Workspace.ActiveTab is not { View.Mode: TabViewMode.WorkingTree } tab
            || (!WorkingPanes.IsDiff(tab.View.FocusedPane) && diff.Search.Phase != DiffSearchPhase.Editing))
        {
            return false;
        }

        SyncWidth();
        Sync();
        return diff.Search.Phase == DiffSearchPhase.Editing
            ? ExecuteInSearchBox(diff, action)
            : ExecuteInDiff(tab, diff, action, count);
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_activeSession()?.WorkingDiff is not { Search.Phase: DiffSearchPhase.Editing } diff)
        {
            return false;
        }

        if (PromptTextEditor.Apply(diff.Search.Text, key) is { } text && text != diff.Search.Text)
        {
            ApplySearch(diff, DiffSearch.Edit(diff.Search, text, diff.Source.Document));
        }

        return true;
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>Call when the shown tab changed: hooks the new tab's diff store and restores its cursor.</summary>
    public void OnTabSwitched(TabSession? previous, TabSession? current)
    {
        if (previous?.WorkingDiff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
            AbandonTypedSearch(old);
        }

        if (_hooked is not null)
        {
            _hooked.StatusRead -= Sync;
        }

        _hooked = current;
        _isRestoring = true;
        try
        {
            _diffList.Source = current?.WorkingDiff?.Source;
            if (current?.WorkingDiff is { } diff)
            {
                diff.Store.Changed += OnStoreChanged;
                current.StatusRead += Sync;
                diff.Source.Width = ListTextWidth();
                if (diff.Source.Count > 0)
                {
                    _diffList.SelectedItem = Math.Min(diff.Source.LineOf(diff.SelectedRow), diff.Source.Count - 1);
                    _diffList.Viewport = _diffList.Viewport with { Y = diff.Source.LineOf(diff.TopRow) };
                }
            }
        }
        finally
        {
            _isRestoring = false;
        }

        Sync();
    }

    /// <summary>
    /// Asks the store for the diff of the file under the cursor of the working-tree view (or a remark when there is none). Does
    /// nothing while the view is not shown. Cheap when the same file is shown already.
    /// </summary>
    public void Sync()
    {
        if (_activeSession()?.WorkingDiff is not { } diff
            || _host.Workspace.ActiveTab is not { View.Mode: TabViewMode.WorkingTree } tab)
        {
            return;
        }

        if (tab.WorkingTree is not { } tree)
        {
            diff.Store.ShowNothing(WorkingTreePaneRenderer.LoadingText);
            return;
        }

        diff.Observe(tree.Snapshot);
        var selection = tree.Selection;
        var list = selection?.Section == WorkingTreeSection.Staged ? tree.Staged : tree.Unstaged;
        if (selection is null || selection.Index >= list.Count)
        {
            diff.Store.ShowNothing(WorkingTreePaneRenderer.NoFileText);
            return;
        }

        var side = selection.Section == WorkingTreeSection.Staged ? WorkingDiffSide.Staged : WorkingDiffSide.Unstaged;
        diff.Store.Show(new WorkingDiffRequest(side, list[selection.Index]), diff.Generation);
    }

    private void OnWorkspaceChanged()
    {
        Sync();
        if (_activeSession()?.WorkingDiff is { Search.Phase: DiffSearchPhase.Editing } diff
            && !WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
        {
            // The pane lost focus (a click) while the search was being typed: keys no longer reach the box, so end it.
            AbandonTypedSearch(diff);
        }
    }

    // Drops a search that is still being typed (not an accepted one) and leaves Insert mode.
    private void AbandonTypedSearch(WorkingDiffSession diff)
    {
        if (diff.Search.Phase != DiffSearchPhase.Editing)
        {
            return;
        }

        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
        _setMode(KeyMode.Normal);
        _diffList.SetNeedsDraw();
    }

    private int ListTextWidth() => PanePadding.TextWidth(_diffList.Viewport.Width, ScreenPadding.Columns(_diffList));

    private DiffPaneModel ModelOf(WorkingDiffSession diff, bool isFocused) => new(
        diff.Source.Document,
        CursorRow(diff),
        diff.Source.RowOf(_diffList.Viewport.Y),
        isFocused,
        diff.Search.Highlight,
        diff.Source.IsWrapped,
        DiffOptionsLabel.Of(diff.Store.Options, diff.Source.IsSideBySide, diff.Source.ShowsSideBySide));

    private bool ExecuteInDiff(RepositoryState tab, WorkingDiffSession diff, ActionId action, int? count)
    {
        var document = diff.Source.Document;
        var row = CursorRow(diff);
        if (ListMotion.TryMove(action, count, _diffList.SelectedItem ?? 0, diff.Source.Count, _diffList.Viewport.Height, out var target))
        {
            SelectLine(target);
        }
        else if (action == ActionIds.DiffToggleWrap)
        {
            diff.Source.SetWrapped(!diff.Source.IsWrapped);
            Select(diff, row, atTop: true);
            Notice = diff.Source.IsWrapped ? "Word wrap on" : "Word wrap off";
        }
        else if (action == ActionIds.DiffToggleSideBySide)
        {
            ToggleSideBySide(diff, row);
        }
        else if (action == ActionIds.DiffCopyLine || action == ActionIds.DiffCopyFile)
        {
            Copy(document, row, action == ActionIds.DiffCopyFile);
        }
        else if (action == ActionIds.DiffLoadAnyway)
        {
            Notice = diff.Store.LoadAnyway() ? "Loading the whole diff..." : "Nothing was cut short";
        }
        else if (TryChangeOptions(diff, action))
        {
            // The diff loads again with the new options.
        }
        else if (action == ActionIds.DiffNextHunk || action == ActionIds.DiffPreviousHunk)
        {
            Jump(diff, document.HunkRows, row, action == ActionIds.DiffNextHunk, count, "hunk");
        }
        else if (action == ActionIds.DiffNextFile || action == ActionIds.DiffPreviousFile)
        {
            Jump(diff, document.FileRows, row, action == ActionIds.DiffNextFile, count, "file");
        }
        else if (action == ActionIds.DiffSearchOpen)
        {
            ApplySearch(diff, DiffSearch.Open());
            _setMode(KeyMode.Insert);
        }
        else if (action == ActionIds.DiffSearchNext || action == ActionIds.DiffSearchPrevious)
        {
            var update = DiffSearch.Step(diff.Search, action == ActionIds.DiffSearchNext, count ?? 1, SearchRow(diff));
            ApplySearch(diff, update);
            Notice = update.State.Notice;
        }
        else if (action == ActionIds.NavigateBack)
        {
            if (diff.Search.IsOpen)
            {
                ApplySearch(diff, DiffSearch.Cancel());
            }
            else
            {
                Back(tab);
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool ExecuteInSearchBox(WorkingDiffSession diff, ActionId action)
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

        _setMode(KeyMode.Normal);
        return true;
    }

    // Back to the list of the side the diff shows.
    private void Back(RepositoryState tab) =>
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(WorkingPanes.ListOf(WorkingPanes.SectionOf(tab.View.FocusedPane)))));

    private void Copy(DiffDocument document, int row, bool wholeFile)
    {
        if (row < 0 || row >= document.Rows.Count || (!wholeFile && document.Rows[row].Kind is DiffRowKind.Note or DiffRowKind.Warning))
        {
            Notice = "Nothing to copy";
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

    private void ToggleSideBySide(WorkingDiffSession diff, int row)
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
            Notice = diff.Source.ShowsSideBySide
                ? "Side by side on"
                : $"Side by side on: needs {needed} columns";
        }
    }

    private bool TryChangeOptions(WorkingDiffSession diff, ActionId action)
    {
        var options = diff.Store.Options;
        DiffOptions changed;
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
            return false;
        }

        Notice = diff.Store.SetOptions(changed) ? DiffOptionsLabel.Describe(changed) : "Context is already at its limit";
        return true;
    }

    private void Jump(WorkingDiffSession diff, IReadOnlyList<int> anchors, int row, bool forward, int? count, string what)
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

    private void ApplySearch(WorkingDiffSession diff, DiffSearchUpdate update)
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

    private void OnStoreChanged()
    {
        if (_activeSession()?.WorkingDiff is { } diff)
        {
            Refresh(diff);
        }
    }

    private void Refresh(WorkingDiffSession diff)
    {
        var document = diff.Store.Document;
        if (!ReferenceEquals(diff.Source.Document, document))
        {
            // A different diff: the old search no longer describes it.
            if (diff.Search.Phase == DiffSearchPhase.Editing)
            {
                _setMode(KeyMode.Normal);
            }

            diff.Search = DiffSearchState.Idle;
            diff.Source.Highlight = null;
            var isSameFile = diff.Store.Request is { } request && request == diff.ShownRequest;
            var cursor = _diffList.SelectedItem ?? 0;
            var top = _diffList.Viewport.Y;
            diff.Source.Update(document);
            diff.ShownRequest = diff.Store.Request;
            if (isSameFile)
            {
                // The same file read again (a refresh): the cursor stays where it was.
                Restore(diff, cursor, top);
            }
            else
            {
                Select(diff, 0, atTop: true);
            }
        }

        _diffList.SetNeedsDraw();
        _refreshChrome();
    }

    private void Restore(WorkingDiffSession diff, int cursor, int top)
    {
        var count = diff.Source.Count;
        if (count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            _diffList.SelectedItem = Math.Clamp(cursor, 0, count - 1);
            _diffList.Viewport = _diffList.Viewport with { Y = Math.Clamp(top, 0, Math.Max(0, count - _diffList.Viewport.Height)) };
        }
        finally
        {
            _isRestoring = false;
        }

        RememberCursor();
    }

    /// <summary>Gives the diff source the list's width; when wrapping is on and the width changed, the cursor and the top row are kept.</summary>
    private void SyncWidth()
    {
        if (_isSyncing || _activeSession()?.WorkingDiff is not { } diff || diff.Source.Width == ListTextWidth())
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
            _isRestoring = true;
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
                _isRestoring = false;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    // Side by side, the cursor row is the first row of a line, but a match may sit on the line's other row: stepping starts from the matched row while the cursor is still on its line.
    private int SearchRow(WorkingDiffSession diff) =>
        diff.SearchRow is { } matched && diff.Source.LineOf(matched) == (_diffList.SelectedItem ?? 0) ? matched : CursorRow(diff);

    private int CursorRow(WorkingDiffSession diff) => diff.Source.RowOf(_diffList.SelectedItem ?? 0);

    private void SelectLine(int line)
    {
        var count = _diffList.Source?.Count ?? 0;
        if (count > 0)
        {
            _diffList.SelectedItem = Math.Clamp(line, 0, count - 1);
            _diffList.EnsureSelectedItemVisible();
        }
    }

    private void Select(WorkingDiffSession diff, int row, bool atTop)
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
            _diffList.Viewport = _diffList.Viewport with { Y = Math.Min(clamped, Math.Max(0, count - _diffList.Viewport.Height)) };
        }
        else
        {
            _diffList.EnsureSelectedItemVisible();
        }
    }

    private void RememberCursor()
    {
        if (!_isRestoring && _activeSession()?.WorkingDiff is { } diff)
        {
            diff.SelectedRow = CursorRow(diff);
            diff.TopRow = diff.Source.RowOf(_diffList.Viewport.Y);
        }
    }
}
