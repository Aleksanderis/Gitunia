using Gitunia.Core.Diff;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Trees;
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
    private const string FilePlaceholder = "Select a file to view its contents";
    private const string SubmodulePlaceholder = "A submodule has no content to show";

    // Long enough that a cursor held down the tree reads only where it rests, short enough not to be felt.
    private static readonly TimeSpan ContentFollowDelay = TimeSpan.FromMilliseconds(40);

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

    /// <summary>The active tab's diff pane shows a file's content, not a diff.</summary>
    public bool IsShowingContent => _activeSession()?.Diff is { ShowsFilesPane: true };

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

        if (focused == PaneKind.Detail && ShowsWithChanges() && _actions.TryScroll(diff, action, count))
        {
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
        if (_activeSession() is { Diff: { } beside } filesSession && IsFilesBeside())
        {
            SyncContent(filesSession, beside);

            return;
        }

        if (_activeSession() is { Diff: { IsShowingContent: true } content } open)
        {
            // A file of one commit's tree stays on screen only while that commit is selected.
            if (open.Details.Current.Commit is { } onGraph && onGraph != content.ShownContentCommit)
            {
                CloseContent(content);
                ReleaseHiddenFocus();
            }

            return;
        }

        if (_activeSession() is not { Diff: { } diff } session
            || !IsShown(diff)
            || session.Details.Current is not { Details: { } loaded, Commit: { } commit }
            || diff.Store.IsShowing(DiffCommit(loaded, commit), ParentToCompare(loaded)))
        {
            return;
        }

        if (ShowsWithChanges() && SetPendingFromCursor(diff))
        {
            // Beside the file list the diff always shows the file or folder under its cursor.
        }
        else if (diff.Store.Status == DiffStatus.Loaded)
        {
            // While another commit is still loading, the document has no files: keep the file remembered so far.
            KeepShown(diff, CurrentFilePath(diff));
        }

        var neighbours = DetailPrefetchPlan.Around(session.Graph, session.Graph.IndexOf(commit));
        diff.Store.Load(
            DiffCommit(loaded, commit),
            ParentToCompare(loaded),
            loaded.IsComparison ? null : neighbours,
            loaded.ParentIndex > 0 || loaded.IsComparison);
    }

    /// <summary>
    /// Opens the content of a file of the Files tab read-only in the diff pane's place and gives the pane focus. The
    /// blob is read off the UI thread (size cap and binary notice: <see cref="FileContentStore"/>).
    /// </summary>
    /// <returns>A notice for the status bar when nothing was opened, empty otherwise.</returns>
    public string OpenFileContent(FileTreeRow row)
    {
        if (_activeSession() is not { Diff: { ContentStore: { } store } diff, Tree: { } tree }
            || _host.Workspace.ActiveTab is not { } tab
            || tree.Store.Current.Commit is not { } commit)
        {
            return "The file tree is still loading";
        }

        if (row.Kind == FileTreeRowKind.Submodule || row.Blob is not { } blob)
        {
            return "A submodule has no content to show";
        }

        var beside = IsFilesBeside();
        if (diff.ShownContentPath == row.Path && diff.ShownContentCommit == commit && (diff.IsOpen || beside))
        {
            // Already open: only take focus, keeping the place in the file.
            _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Diff)));

            return string.Empty;
        }

        _actions.ForgetSearch(diff);
        diff.ShownContentPath = row.Path;
        diff.ShownContentCommit = commit;
        diff.IsContentPlaceholder = false;
        diff.IsOpen = !beside;
        diff.ZoomedPane = PaneLayout.CanOpenDiff(_screenSize().Height) ? null : PaneKind.Diff;
        store.Load(commit, row.Path, blob, row.Size);
        ShowContent(diff, store);
        _relayout();
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Diff)));

        return string.Empty;
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
        if (_activeSession() is { Diff: { } beside } filesSession && IsFilesBeside())
        {
            SyncContent(filesSession, beside);

            return;
        }

        if (_activeSession() is not { Diff: { } diff } session
            || !ShowsWithChanges()
            || !SetPendingFromCursor(diff))
        {
            return;
        }

        if (diff.Store.Status == DiffStatus.Loaded
            && session.Details.Current is { Commit: { } commit, Details: { } shown }
            && diff.Store.Commit == DiffCommit(shown, commit))
        {
            Refresh(diff);
        }
    }

    /// <summary>The diff pane's title row for the active tab.</summary>
    public StyledLine TitleLine(int width, GraphGlyphSet glyphs, ChipKeys? keys = null) =>
        DiffPaneRenderer.RenderTitle(ModelOf(_activeSession()?.Diff, _diffList.HasFocus), width, glyphs, keys);

    /// <summary>Call when the shown tab changed: hooks the new tab's diff store and restores its cursor.</summary>
    public void OnTabSwitched(TabSession? previous, TabSession? current)
    {
        if (previous?.Diff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
            if (old.ContentStore is { } oldContent)
            {
                oldContent.Changed -= OnContentChanged;
            }

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
                if (diff.ContentStore is { } content)
                {
                    content.Changed += OnContentChanged;
                }

                _actions.RestoreRemembered(diff);
            }
        }
        finally
        {
            _actions.IsRestoring = false;
        }

        if (current?.Diff is { IsShowingContent: true, ContentStore: { } shown })
        {
            ShowContent(current.Diff, shown);
        }
        else if (current?.Diff is { } open && IsShown(open))
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
        if (_activeSession()?.Diff is { ShowsFilesPane: true } content
            && _host.Workspace.ActiveTab?.View.BottomTab != BottomTab.Files)
        {
            // The file content belongs to the Files tab: another tab shows its own pane.
            CloseContent(content);
        }

        if (_activeSession() is { Diff: { } shown } filesSession && IsFilesBeside())
        {
            // The Files tab was selected: the pane shows what is under the tree's cursor.
            SyncContent(filesSession, shown);
        }

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
            : _actions.ModelOf(diff, diff.Store.Options, isFocused) with
            {
                IsFolder = diff.BuiltFolderPath is not null,
                FileNumber = diff.BuiltFolderPath is null ? diff.ShownFile + 1 : 0,
                FileCount = diff.BuiltFolderPath is null
                    ? diff.ShownFull?.FilePaths.Count ?? 0
                    : diff.ShownDocument.FilePaths.Count,
            };

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

        if (diff.ShowsFilesPane && (action == ActionIds.DiffLoadAnyway || IsOptionAction(diff, action)))
        {
            Notice = "The file content has no diff options";
        }
        else if (action == ActionIds.DiffLoadAnyway)
        {
            LoadAnyway(diff);
        }
        else if (TryChangeOptions(diff, action))
        {
            // The diff loads again with the new options; the cursor returns to the same file.
        }
        else if (action == ActionIds.DiffNextFile || action == ActionIds.DiffPreviousFile)
        {
            MoveToFile(diff, action == ActionIds.DiffNextFile, count ?? 1);
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

    private bool IsFilesBeside() =>
        _host.Workspace.ActiveTab?.View.BottomTab == BottomTab.Files && ShowsWithChanges();

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

        var index = content.FileIndexAt(_detailList.SelectedItem ?? -1);
        return index >= 0 && index < content.Files.Count ? content.Files[index].Path : null;
    }

    /// <summary>The path of the folder under the cursor of the Changes list (tree style), null on any other
    /// row.</summary>
    private string? SelectedFolderPath() =>
        _activeSession()?.DetailSource is { Content: { Tab: BottomTab.Changes } content }
            ? content.FolderAt(_detailList.SelectedItem ?? -1)
            : null;

    // Asks for the file or folder under the file list's cursor; false when the cursor is on neither.
    private bool SetPendingFromCursor(DiffSession diff)
    {
        if (SelectedFolderPath() is { } folder)
        {
            diff.PendingFolderPath = folder;
            diff.PendingFilePath = null;

            return true;
        }

        if (SelectedFilePath() is { } path)
        {
            diff.PendingFilePath = path;
            diff.PendingFolderPath = null;

            return true;
        }

        return false;
    }

    // Shows the same thing again after a reload: the folder when one is shown, else the file.
    private static void KeepShown(DiffSession diff, string? filePath)
    {
        if (diff.ShownFolderPath is { } folder)
        {
            diff.PendingFolderPath = folder;
            diff.PendingFilePath = null;
        }
        else
        {
            diff.PendingFilePath = filePath;
            diff.PendingFolderPath = null;
        }
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

        if (!SetPendingFromCursor(diff))
        {
            Notice = "Select a file or folder to see its diff";
            return;
        }

        // Beside the file list the diff is part of the tab already; only narrow or short screens open it on demand.
        diff.IsOpen = !ShowsWithChanges();
        diff.ZoomedPane = PaneLayout.CanOpenDiff(_screenSize().Height) ? null : PaneKind.Diff;
        var started = diff.Store.Load(
            DiffCommit(loaded, commit),
            ParentToCompare(loaded),
            isAlternateParent: loaded.ParentIndex > 0 || loaded.IsComparison);
        _relayout();
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Diff)));
        if (!started)
        {
            // The same commit is already loaded: only the cursor moves to the chosen file.
            Refresh(diff);
        }
    }

    /// <summary>
    /// The parent the diff is read against: the first one, the one a merge was switched to, or the older commit of a
    /// comparison.
    /// </summary>
    private static ObjectId? ParentToCompare(CommitDetails details) =>
        details.CompareOlder
        ?? (details.Parents.Count > details.ParentIndex ? details.Parents[details.ParentIndex] : null);

    /// <summary>The commit the diff shows: the selected one, or the newer commit of a comparison.</summary>
    private static ObjectId DiffCommit(CommitDetails details, ObjectId selected) => details.CompareNewer ?? selected;

    private static bool IsOptionAction(DiffSession diff, ActionId action) =>
        DiffListActions.TryResolveOptions(diff.Store.Options, action, out _);

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

        KeepShown(diff, path);
        Notice = DiffOptionsLabel.Describe(changed);
        return true;
    }

    private void LoadAnyway(DiffSession diff)
    {
        var path = CurrentFilePath(diff);
        if (diff.Store.LoadAnyway())
        {
            KeepShown(diff, path);
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

    // ]f and [f pick the next or previous file of the commit; the pane then shows that file only. The file list's
    // cursor follows, so the list and the diff stay on the same file. Past the first or last file nothing moves.
    private void MoveToFile(DiffSession diff, bool forward, int count)
    {
        if (diff.ShowsFilesPane)
        {
            MoveToFileRow(diff, forward, count);

            return;
        }

        if (diff.BuiltFolderPath is not null)
        {
            MoveToFileOfFolder(diff, forward, count);
            return;
        }

        var full = diff.ShownFull;
        var target = full is null || diff.ShownFile < 0
            ? -1
            : Math.Clamp(diff.ShownFile + (forward ? count : -count), 0, full.FilePaths.Count - 1);
        if (full is null || target < 0 || target == diff.ShownFile)
        {
            Notice = forward ? "No next file" : "No previous file";
            return;
        }

        var path = full.FilePaths[target][0];
        if (_activeSession()?.DetailSource is { Content: { Tab: BottomTab.Changes } content })
        {
            for (var index = 0; index < content.Files.Count; index++)
            {
                if (content.Files[index].Path == path && content.RowOfFile(index) is >= 0 and var row)
                {
                    _detailList.SelectedItem = row;
                    _detailList.EnsureSelectedItemVisible();
                    break;
                }
            }
        }

        diff.PendingFilePath = path;
        diff.PendingFolderPath = null;
        Refresh(diff);
    }

    // In the folder view the pane holds several files: ]f and [f move the cursor from file to file inside it.
    private void MoveToFileOfFolder(DiffSession diff, bool forward, int count)
    {
        var document = diff.ShownDocument;
        var row = _actions.CursorRow(diff);
        var current = row >= 0 && row < document.Rows.Count ? Math.Max(0, document.Rows[row].FileIndex) : 0;
        var target = Math.Clamp(current + (forward ? count : -count), 0, document.FileRows.Count - 1);
        if (target == current)
        {
            Notice = forward ? "No next file" : "No previous file";
            return;
        }

        _actions.Select(diff, document.FileRows[target], atTop: true);
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
        diff.PendingFolderPath = null;
        diff.ShownFolderPath = null;
        diff.BuiltFolderPath = null;
        diff.ShownContentPath = null;
        diff.ShownContentCommit = null;
        diff.IsContentPlaceholder = false;
        diff.ContentStore?.Clear();
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

    private void OnContentChanged()
    {
        if (_activeSession()?.Diff is { IsShowingContent: true, ContentStore: { } store } diff
            && store.Current.Path == diff.ShownContentPath
            && store.Current.Commit == diff.ShownContentCommit)
        {
            ShowContent(diff, store);
        }
    }

    // Puts the document of the open file (its text, or a note while it loads or when it cannot be shown) in the pane.
    private void ShowContent(DiffSession diff, FileContentStore store)
    {
        if (!ReferenceEquals(diff.Source.Document, store.Current.Document))
        {
            _actions.ForgetSearch(diff);
            diff.Source.Update(store.Current.Document);
            _actions.Select(diff, 0, atTop: true);
        }

        _diffList.SetNeedsDraw();
        _refreshChrome();
    }

    // Leaves the file content without touching the loaded diff: the pane is empty until the diff shows again.
    private void CloseContent(DiffSession diff)
    {
        diff.ShownContentPath = null;
        diff.ShownContentCommit = null;
        diff.IsContentPlaceholder = false;
        diff.ContentStore?.Clear();
        diff.IsOpen = false;
        diff.ZoomedPane = null;
        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
        diff.ShownFull = null;
        diff.ShownDocument = DiffDocument.Empty;
        diff.Source.Update(DiffDocument.Empty);
        if (ShowsWithChanges())
        {
            Refresh(diff);
        }

        _relayout();
        _refreshChrome();
    }

    // The Files tab beside the tree shows what is under its cursor: a file's content (read off the UI thread; the old
    // content stays until the new one arrives), else a placeholder.
    private void SyncContent(TabSession session, DiffSession diff)
    {
        if (diff.ContentStore is not { } store)
        {
            return;
        }

        var row = TreeRowUnderCursor(session);
        var commit = session.Tree?.Store.Current.Commit;
        if (row is { Kind: not FileTreeRowKind.Directory and not FileTreeRowKind.Submodule, Blob: { } blob }
            && commit is { } shown
            && session.Details.Current.Commit == shown)
        {
            if (diff.ShownContentPath == row.Path && diff.ShownContentCommit == shown)
            {
                return;
            }

            diff.ShownContentPath = row.Path;
            diff.ShownContentCommit = shown;
            diff.IsContentPlaceholder = false;
            store.Load(shown, row.Path, blob, row.Size, ContentFollowDelay);
            _refreshChrome();

            return;
        }

        ShowPlaceholder(diff, row?.Kind == FileTreeRowKind.Submodule ? SubmodulePlaceholder : FilePlaceholder);
    }

    private void ShowPlaceholder(DiffSession diff, string text)
    {
        if (diff.IsContentPlaceholder && diff.Source.Document.Rows is [{ } only] && only.Text == text)
        {
            return;
        }

        diff.ShownContentPath = null;
        diff.ShownContentCommit = null;
        diff.IsContentPlaceholder = true;
        diff.ContentStore?.Clear();
        _actions.ForgetSearch(diff);
        diff.Source.Update(FileContentDocument.Placeholder(text));
        _actions.Select(diff, 0, atTop: true);
        _diffList.SetNeedsDraw();
        _refreshChrome();
    }

    private FileTreeRow? TreeRowUnderCursor(TabSession session)
    {
        var rows = session.DetailSource.Content is { Tab: BottomTab.Files } content ? content.Tree : [];
        var index = _detailList.SelectedItem ?? -1;

        return index >= 0 && index < rows.Count ? rows[index] : null;
    }

    // ]f and [f in the file content pane: the tree's cursor moves to the next or previous file row and the pane follows.
    private void MoveToFileRow(DiffSession diff, bool forward, int count)
    {
        if (_activeSession() is not { } session || !IsFilesBeside())
        {
            Notice = "The file content has no other files to jump to";

            return;
        }

        var rows = session.DetailSource.Content.Tree;
        var step = forward ? 1 : -1;
        var target = -1;
        var remaining = count;
        var start = _detailList.SelectedItem ?? -1;
        for (var next = start + step; next >= 0 && next < rows.Count && remaining > 0; next += step)
        {
            if (rows[next].Kind != FileTreeRowKind.Directory)
            {
                target = next;
                remaining--;
            }
        }

        if (target < 0)
        {
            Notice = forward ? "No next file" : "No previous file";

            return;
        }

        _detailList.SelectedItem = target;
        _detailList.EnsureSelectedItemVisible();
        SyncContent(session, diff);
    }

    private void Refresh(DiffSession diff)
    {
        if (diff.ShowsFilesPane || IsFilesBeside())
        {
            return;
        }

        var full = diff.Store.Document;
        var isLoaded = diff.Store.Status == DiffStatus.Loaded;
        var path = isLoaded ? diff.PendingFilePath : null;
        if (path is not null)
        {
            diff.PendingFilePath = null;
            diff.ShownFolderPath = null;
        }

        if (isLoaded && diff.PendingFolderPath is { } pendingFolder)
        {
            diff.PendingFolderPath = null;
            diff.ShownFolderPath = pendingFolder;
        }

        var folderFiles = isLoaded && diff.ShownFolderPath is { } folder ? full.FileIndexesUnder(folder) : [];
        if (isLoaded && folderFiles.Count == 0)
        {
            // The folder has no change in this commit: the pane falls back to a single file.
            diff.ShownFolderPath = null;
        }

        var wanted = path ?? diff.ShownPath;
        var file = folderFiles.Count > 0
            ? folderFiles[0]
            : full.FilePaths.Count == 0 ? -1 : Math.Max(0, wanted is null ? 0 : full.FileIndexOf(wanted));
        var builtFolder = folderFiles.Count > 0 ? diff.ShownFolderPath : null;
        if (!ReferenceEquals(diff.ShownFull, full) || diff.ShownFile != file || diff.BuiltFolderPath != builtFolder)
        {
            // The pane holds one file only, the one under the file list's cursor, or the files of the folder there.
            diff.ShownFull = full;
            diff.ShownFile = file;
            diff.BuiltFolderPath = builtFolder;
            diff.ShownPath = file >= 0 ? full.FilePaths[file][0] : null;
            diff.ShownDocument = folderFiles.Count > 0
                ? full.ForFiles(folderFiles)
                : file >= 0 ? full.ForFile(file) : full;
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
