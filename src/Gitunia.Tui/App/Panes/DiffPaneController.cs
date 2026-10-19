using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// Runs the diff pane of the active tab. The diff is part of the Changes tab: where the screen has room the pane is always shown
/// beside the file list and follows its cursor (see <see cref="PaneLayout.ShowsDiffWithChanges"/>); on narrow or short screens
/// <c>Enter</c> opens it on demand. It scrolls and jumps through the diff, searches it and zooms panes. It is also the diff's search prompt (the box in Insert mode), so it plugs into the same prompt list as the
/// graph search. Everything it changes about focus goes through workspace commands.
/// </summary>
public sealed class DiffPaneController : IPromptHandler
{
    private const string SideBySideHint = " (zoom with <space>z)";

    private readonly WorkspaceHost _host;
    private readonly Func<TabSession?> _activeSession;
    private readonly ListView _detailList;
    private readonly ListView _diffList;
    private readonly Func<ScreenSize> _screenSize;
    private readonly Action _relayout;
    private readonly Action _refreshChrome;
    private readonly DiffListActions _actions;

    /// <param name="host">The workspace the focus commands go to.</param>
    /// <param name="activeSession">The active tab's runtime session.</param>
    /// <param name="detailList">The detail pane's list, where a file is picked.</param>
    /// <param name="diffList">The diff pane's list.</param>
    /// <param name="setMode">Switches the input mode (Insert while the search box is open).</param>
    /// <param name="screenSize">Terminal columns and rows, to tell whether the diff fits next to the file list and the graph.</param>
    /// <param name="relayout">Asks the views to lay out again after the pane arrangement changed.</param>
    /// <param name="refreshChrome">Redraws the pane titles and the status bar after the diff changed on its own (it finished loading).</param>
    /// <param name="clipboard">Copies diff text.</param>
    public DiffPaneController(
        WorkspaceHost host,
        Func<TabSession?> activeSession,
        ListView detailList,
        ListView diffList,
        Action<KeyMode> setMode,
        Func<ScreenSize> screenSize,
        Action relayout,
        Action refreshChrome,
        ClipboardCopier clipboard)
    {
        _host = host;
        _activeSession = activeSession;
        _detailList = detailList;
        _diffList = diffList;
        _screenSize = screenSize;
        _relayout = relayout;
        _refreshChrome = refreshChrome;
        _actions = new DiffListActions(
            diffList,
            setMode,
            clipboard,
            () => activeSession()?.Diff,
            SideBySideHint);
        host.Changed += OnWorkspaceChanged;
    }

    public string Notice
    {
        get => _actions.Notice;
        private set => _actions.Notice = value;
    }

    public string StatusLine => _actions.StatusLine(_activeSession()?.Diff);

    /// <summary>The panes' arrangement for the active tab; a zoom that does not match the focused pane is not part of it. Has no side effects (see <see cref="ReleaseStaleZoom"/>).</summary>
    public PaneArrangement Arrangement()
    {
        if (_activeSession()?.Diff is not { } diff)
        {
            return PaneArrangement.Normal with { Sizes = SizesOfActiveTab() };
        }

        var zoomed = diff.ZoomedPane == _host.Workspace.ActiveTab?.View.FocusedPane ? diff.ZoomedPane : null;
        return new PaneArrangement(IsShown(diff), zoomed, SizesOfActiveTab());
    }

    /// <summary>The pane sizes saved in the active tab's view state.</summary>
    public PaneSizes SizesOfActiveTab() => _host.Workspace.ActiveTab?.View is { } view ? new PaneSizes(view.SidebarWidth, view.BottomHeight, view.DetailWidth) : PaneSizes.Automatic;

    /// <summary>Forgets the active tab's zoom when focus moved to another pane, so returning to the zoomed pane does not zoom it again. Runs after every workspace command.</summary>
    public void ReleaseStaleZoom()
    {
        if (_activeSession()?.Diff is { ZoomedPane: { } zoomed } diff && zoomed != _host.Workspace.ActiveTab?.View.FocusedPane)
        {
            diff.ZoomedPane = null;
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        _actions.SyncWidth();
        FollowSelection();
        if (_activeSession()?.Diff is not { } diff || _host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var focused = tab.View.FocusedPane;
        if (action == ActionIds.PaneZoom)
        {
            Zoom(diff, focused);
            return true;
        }

        if (action == ActionIds.DetailOpenDiff && focused == PaneKind.Detail)
        {
            OpenSelectedFile(tab, diff);
            return true;
        }

        return focused == PaneKind.Diff && ExecuteInDiff(tab, diff, action, count);
    }

    /// <summary>The quick-jump target of the active tab's diff, null while it has none.</summary>
    public IJumpTarget? JumpTarget() => _activeSession()?.Diff is { } diff ? _actions.JumpTarget(PaneKind.Diff, diff) : null;

    public bool HandleTextKey(KeyEvent key)
    {
        var diff = _activeSession()?.Diff;

        return _actions.HandleTextKey(diff, diff?.Source.Document ?? DiffDocument.Empty, key);
    }

    /// <summary>
    /// Makes an open diff follow the graph selection: when the selected commit's details have arrived and differ from the commit
    /// the diff shows, the diff loads that commit and keeps the file under the cursor when the new commit has it. Does nothing while
    /// the diff is closed or the details are still loading.
    /// </summary>
    public void FollowSelection()
    {
        if (_activeSession() is not { Diff: { } diff } session
            || !IsShown(diff)
            || session.Details.Current is not { Details: { } loaded, Commit: { } commit }
            || diff.Store.Commit == commit)
        {
            return;
        }

        if (ShowsWithChanges() && SelectedFilePath() is { } selected)
        {
            // Beside the file list the diff always shows the file under its cursor.
            diff.PendingFilePath = selected;
        }
        else if (diff.Store.Status == DiffStatus.Loaded)
        {
            // While another commit is still loading, the document has no files: keep the file remembered so far.
            diff.PendingFilePath = CurrentFilePath(diff);
        }

        var neighbours = DetailPrefetchPlan.Around(session.Graph, session.Graph.IndexOf(commit));
        diff.Store.Load(commit, loaded.Parents.Count > 0 ? loaded.Parents[0] : null, neighbours);
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>The terminal changed size: loads the diff that now has room, and takes focus off a diff pane that no longer fits.</summary>
    public void OnScreenResized()
    {
        ReleaseHiddenFocus();
        FollowSelection();
    }

    /// <summary>
    /// The file list's cursor moved: the diff beside it shows that file at once (the whole commit is loaded already, so this only
    /// scrolls; while the commit is still loading the file is remembered and shown when it arrives).
    /// </summary>
    public void OnDetailCursorMoved()
    {
        if (_activeSession() is not { Diff: { } diff } session
            || !ShowsWithChanges()
            || SelectedFilePath() is not { } path)
        {
            return;
        }

        diff.PendingFilePath = path;
        if (diff.Store.Status == DiffStatus.Loaded && session.Details.Current is { Commit: { } commit } && diff.Store.Commit == commit)
        {
            Refresh(diff);
        }
    }

    /// <summary>The diff pane's title row for the active tab.</summary>
    public StyledLine TitleLine(int width, GraphGlyphSet glyphs) =>
        DiffPaneRenderer.RenderTitle(ModelOf(_activeSession()?.Diff, _diffList.HasFocus), width, glyphs);

    /// <summary>Call when the shown tab changed: hooks the new tab's diff store and restores its cursor.</summary>
    public void OnTabSwitched(TabSession? previous, TabSession? current)
    {
        if (previous?.Diff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
            old.Store.ReleaseCache();
            _actions.AbandonTypedSearch(old);
        }

        _actions.IsRestoring = true;
        try
        {
            _diffList.Source = current?.Diff?.Source;
            if (current?.Diff is { } diff)
            {
                diff.Store.Changed += OnStoreChanged;
                _actions.RestoreRemembered(diff);
            }
        }
        finally
        {
            _actions.IsRestoring = false;
        }

        if (current?.Diff is { } open && IsShown(open))
        {
            // The load may have finished while the tab was in the background.
            Refresh(open);
        }

        _relayout();
        FollowSelection();
    }

    private void OnWorkspaceChanged()
    {
        ReleaseStaleZoom();
        if (ReleaseHiddenFocus())
        {
            return;
        }

        if (_activeSession()?.Diff is { Search.Phase: DiffSearchPhase.Editing } diff && _host.Workspace.ActiveTab?.View.FocusedPane != PaneKind.Diff)
        {
            // The pane lost focus (a click) while the search was being typed: keys no longer reach the box, so end it.
            _actions.AbandonTypedSearch(diff);
        }
    }

    private DiffPaneModel ModelOf(DiffSession? diff, bool isFocused) =>
        diff is null
            ? new DiffPaneModel(DiffDocument.Empty, 0, 0, isFocused)
            : _actions.ModelOf(diff, diff.Store.Options, isFocused);

    private bool ExecuteInDiff(RepositoryState tab, DiffSession diff, ActionId action, int? count)
    {
        if (diff.Search.Phase == DiffSearchPhase.Editing)
        {
            return _actions.ExecuteInSearchBox(diff, action);
        }

        if (_actions.TryExecuteView(diff, action, count))
        {
            return true;
        }

        if (action == ActionIds.DiffLoadAnyway)
        {
            LoadAnyway(diff);
        }
        else if (TryChangeOptions(diff, action))
        {
            // The diff loads again with the new options; the cursor returns to the same file.
        }
        else if (action == ActionIds.DiffNextFile || action == ActionIds.DiffPreviousFile)
        {
            Notice = "The diff shows the selected file only; pick another in the file list";
        }
        else if (action == ActionIds.NavigateBack)
        {
            if (diff.Search.IsOpen)
            {
                _actions.ApplySearch(diff, DiffSearch.Cancel());
            }
            else
            {
                Close(tab, diff);
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    // The Changes tab was left (or the screen shrank) while the diff had focus: the diff is hidden now, so focus goes back to the file list.
    private bool ReleaseHiddenFocus()
    {
        if (_activeSession()?.Diff is not { } shown || _host.Workspace.ActiveTab is not { } active || active.View.FocusedPane != PaneKind.Diff || IsShown(shown))
        {
            return false;
        }

        _host.Dispatch(new TabCommand(active.Id, new FocusPaneCommand(PaneKind.Detail)));
        return true;
    }

    /// <summary>The diff pane is on screen: opened on demand, or shown by the Changes tab.</summary>
    private bool IsShown(DiffSession diff) => diff.IsOpen || ShowsWithChanges();

    private bool ShowsWithChanges()
    {
        var size = _screenSize();
        var width = size.Width;
        var height = size.Height;
        return _host.Workspace.ActiveTab is { } tab && PaneLayout.ShowsDiffWithChanges(width, height, tab.View.BottomTab);
    }

    /// <summary>The path of the file under the cursor of the Changes list, null when the cursor is not on a file.</summary>
    private string? SelectedFilePath()
    {
        if (_activeSession()?.DetailSource is not { Content: { Tab: BottomTab.Changes } content })
        {
            return null;
        }

        var index = (_detailList.SelectedItem ?? -1) - content.FirstFileRow;
        return index >= 0 && index < content.Files.Count ? content.Files[index].Path : null;
    }

    private void OpenSelectedFile(RepositoryState tab, DiffSession diff)
    {
        var details = _activeSession()?.Details.Current;
        var source = _activeSession()?.DetailSource;
        if (details?.Details is not { } loaded || details.Commit is not { } commit || source is null)
        {
            Notice = "The commit details are still loading";
            return;
        }

        if (source.Content.Tab != BottomTab.Changes)
        {
            Notice = "Open the Changes tab (gc) to pick a file";
            return;
        }

        var index = (_detailList.SelectedItem ?? -1) - source.Content.FirstFileRow;
        if (index < 0 || index >= source.Content.Files.Count)
        {
            Notice = "Select a file to see its diff";
            return;
        }

        // Beside the file list the diff is part of the tab already; only narrow or short screens open it on demand.
        diff.IsOpen = !ShowsWithChanges();
        diff.PendingFilePath = source.Content.Files[index].Path;
        diff.ZoomedPane = PaneLayout.CanOpenDiff(_screenSize().Height) ? null : PaneKind.Diff;
        var started = diff.Store.Load(commit, loaded.Parents.Count > 0 ? loaded.Parents[0] : null);
        _relayout();
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Diff)));
        if (!started)
        {
            // The same commit is already loaded: only the cursor moves to the chosen file.
            Refresh(diff);
        }
    }

    private bool TryChangeOptions(DiffSession diff, ActionId action)
    {
        if (!DiffListActions.TryResolveOptions(diff.Store.Options, action, out var changed))
        {
            return false;
        }

        var path = CurrentFilePath(diff);
        if (!diff.Store.SetOptions(changed))
        {
            Notice = "Context is already at its limit";
            return true;
        }

        diff.PendingFilePath = path;
        Notice = DiffOptionsLabel.Describe(changed);
        return true;
    }

    private void LoadAnyway(DiffSession diff)
    {
        var path = CurrentFilePath(diff);
        if (diff.Store.LoadAnyway())
        {
            diff.PendingFilePath = path;
            Notice = "Loading the whole diff...";
        }
        else
        {
            Notice = "Nothing was cut short";
        }
    }

    private string? CurrentFilePath(DiffSession diff)
    {
        var document = diff.Source.Document;
        var row = _actions.CursorRow(diff);
        var file = row >= 0 && row < document.Rows.Count ? document.Rows[row].FileIndex : -1;
        return file >= 0 && file < document.FilePaths.Count ? document.FilePaths[file][0] : null;
    }

    private void Close(RepositoryState tab, DiffSession diff)
    {
        if (ShowsWithChanges())
        {
            // The diff belongs to the Changes tab and stays: going back only returns to the file list.
            diff.IsOpen = false;
            diff.ZoomedPane = null;
            _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Detail)));
            _relayout();
            return;
        }

        diff.IsOpen = false;
        diff.ZoomedPane = null;
        diff.PendingFilePath = null;
        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
        diff.Store.Clear();
        diff.ShownFull = null;
        diff.ShownPath = null;
        diff.ShownDocument = DiffDocument.Empty;
        diff.Source.Update(DiffDocument.Empty);
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Detail)));
        _relayout();
    }

    private void Zoom(DiffSession diff, PaneKind focused)
    {
        if (!PaneZoom.CanZoom(focused))
        {
            Notice = "The sidebar cannot be zoomed";
            return;
        }

        diff.ZoomedPane = PaneZoom.Toggle(Arrangement().Zoomed, focused);
        _relayout();
    }

    private void OnStoreChanged()
    {
        if (_activeSession()?.Diff is { } diff)
        {
            Refresh(diff);
        }
    }

    private void Refresh(DiffSession diff)
    {
        var full = diff.Store.Document;
        var path = diff.Store.Status == DiffStatus.Loaded ? diff.PendingFilePath : null;
        if (path is not null)
        {
            diff.PendingFilePath = null;
        }

        var wanted = path ?? diff.ShownPath;
        var file = full.FilePaths.Count == 0 ? -1 : Math.Max(0, wanted is null ? 0 : full.FileIndexOf(wanted));
        if (!ReferenceEquals(diff.ShownFull, full) || diff.ShownFile != file)
        {
            // The pane holds one file only: the one under the file list's cursor.
            diff.ShownFull = full;
            diff.ShownFile = file;
            diff.ShownPath = file >= 0 ? full.FilePaths[file][0] : null;
            diff.ShownDocument = file >= 0 ? full.ForFile(file) : full;
        }

        if (!ReferenceEquals(diff.Source.Document, diff.ShownDocument))
        {
            _actions.ForgetSearch(diff);
            diff.Source.Update(diff.ShownDocument);
            _actions.Select(diff, 0, atTop: true);
        }

        _diffList.SetNeedsDraw();
        _refreshChrome();
    }
}
