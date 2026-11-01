using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Staging;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Diff.Models;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Search;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Runs the diff box of the Changes view of the active tab. It follows the file under the cursor of the focused list (the
/// Changes view diff for a file in Unstaged, the index diff for one in Staged), loads it off the UI thread through the tab's
/// <see cref="WorkingDiffStore"/> and shows it in the same widget as the history diff: scrolling, hunk jumps, wrap, side by side,
/// context, search and copy. It is also the diff's search prompt, so it plugs into the same prompt list as the graph search.
/// Everything it changes about focus goes through workspace commands.
/// </summary>
public sealed class WorkingDiffController : IPromptHandler
{
    private readonly Dispatcher _host;
    private readonly Func<TerminalTabSession?> _activeSession;
    private readonly ListView _diffList;
    private readonly Action _refreshChrome;
    private readonly Action _relayout;
    private readonly DiffListActions _actions;
    private TerminalTabSession? _hooked;
    private int? _hunkAfterReload;
    private int? _changeAfterReload;

    /// <param name="host">The workspace the focus commands go to.</param>
    /// <param name="activeSession">The active tab's runtime session.</param>
    /// <param name="diffList">The list of the diff box.</param>
    /// <param name="refreshChrome">Redraws the frame titles and the status bar after the diff changed on its own (it finished loading).</param>
    /// <param name="clipboard">Copies diff text.</param>
    /// <param name="relayout">Lays the panes out again after the zoom changed; null in tests without a screen.</param>
    public WorkingDiffController(
        Dispatcher host,
        Func<TerminalTabSession?> activeSession,
        ListView diffList,
        Action refreshChrome,
        ClipboardCopier clipboard,
        Action? relayout = null)
    {
        _host = host;
        _activeSession = activeSession;
        _diffList = diffList;
        _refreshChrome = refreshChrome;
        _relayout = relayout ?? (() => { });
        _actions = new DiffListActions(diffList, host, clipboard, () => activeSession()?.WorkingDiff, string.Empty, "change");
        host.Changed += OnWorkspaceChanged;
    }

    public string Notice
    {
        get => _actions.Notice;
        private set => _actions.Notice = value;
    }

    /// <summary>True while the diff of the shown tab fills the whole Changes view (the diff has focus and was zoomed).</summary>
    public bool IsZoomed =>
        _activeSession()?.WorkingDiff is { IsZoomed: true }
        && WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph);

    public string StatusLine => _actions.StatusLine(_activeSession()?.WorkingDiff);

    /// <summary>The title of the diff box: the side, the hunk the cursor is in and the file, plus the markers of the diff options.</summary>
    /// <param name="isRepeating">The repeat mode is on for the diff box: the title shows its marker.</param>
    public string TitleText(bool isRepeating = false)
    {
        var tab = _host.Workspace.ActiveTab;
        var model = default(DiffPaneModel);
        if (_activeSession()?.WorkingDiff is { } diff)
        {
            model = ModelOf(diff, WorkingPanes.IsDiff(tab?.View.FocusedPane ?? PaneKind.Graph)) with
            {
                IsRepeating = isRepeating,
            };
        }

        return WorkingTreePaneRenderer.DiffTitle(tab?.WorkingTree, model);
    }

    /// <summary>The diff box as the frame composer draws it, null while the tab has no diff.</summary>
    public DiffPaneModel? Model()
    {
        if (_activeSession()?.WorkingDiff is not { } diff)
        {
            return null;
        }

        return ModelOf(diff, WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph));
    }

    /// <summary>The quick-jump target of the diff box of the shown tab, null while it has none.</summary>
    public IJumpTarget? JumpTarget()
    {
        if (_activeSession()?.WorkingDiff is { } diff
            && _host.Workspace.ActiveTab?.View.FocusedPane is { } pane
            && WorkingPanes.IsDiff(pane))
        {
            return _actions.JumpTarget(pane, diff);
        }

        return null;
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_activeSession()?.WorkingDiff is { } shown
            && _host.Workspace.ActiveTab is { View.Mode: TabViewMode.WorkingTree } listTab
            && WorkingPanes.IsList(listTab.View.FocusedPane)
            && _actions.TryScroll(shown, action, count))
        {
            return true;
        }

        if (_activeSession()?.WorkingDiff is not { } diff
            || _host.Workspace.ActiveTab is not { View.Mode: TabViewMode.WorkingTree } tab
            || (!WorkingPanes.IsDiff(tab.View.FocusedPane) && diff.Search.Phase != DiffSearchPhase.Editing))
        {
            return false;
        }

        _actions.SyncWidth();
        Sync();
        bool isHandled;
        if (diff.Search.Phase == DiffSearchPhase.Editing)
        {
            isHandled = _actions.ExecuteInSearchBox(diff, action);
        }
        else
        {
            isHandled = ExecuteInDiff(tab, diff, action, count);
        }

        if (isHandled)
        {
            // Any other key of the diff is the user moving on: the cursor no longer waits to jump to a hunk.
            _hunkAfterReload = null;
            _changeAfterReload = null;
        }

        return isHandled;
    }

    /// <summary>
    /// Reads what to stage or unstage from the shown diff: the Visual range when there is one, else the hunk under the cursor (a
    /// file without hunks, such as a binary one, goes whole). Refuses while the diff is loading or cut short, for a conflicted
    /// file and for a diff that holds no file.
    /// </summary>
    /// <param name="range">The Visual range in list lines, null for the hunk under the cursor.</param>
    public DiffStagingCapture CaptureStaging(RowRange? range)
    {
        if (_activeSession()?.WorkingDiff is not { } diff || diff.Store.Request is not { } target)
        {
            return DiffStagingCapture.Refused("There is no diff to stage from");
        }

        var document = diff.Source.Document;
        if (diff.Store.Status != DiffStatus.Loaded)
        {
            return DiffStagingCapture.Refused("The diff is still loading");
        }

        if (diff.Store.IsCut)
        {
            return DiffStagingCapture.Refused("The diff is cut short: press <space>vl to load all of it first");
        }

        if (target.Folder is not null)
        {
            return DiffStagingCapture.Refused(
                "This is the diff of a folder: pick one of its files to stage a hunk or lines");
        }

        if (target.Entry.Kind == StatusEntryKind.Unmerged)
        {
            return DiffStagingCapture.Refused($"{WorkingTreeRowRenderer.Printable(target.Entry.Path)} is conflicted: resolve it first");
        }

        if (document.FileRows.Count == 0)
        {
            return DiffStagingCapture.Refused("There is nothing to stage here");
        }

        var row = _actions.CursorRow(diff);
        var hunkIndex = DiffSelection.HunkIndexAt(document, row);
        if (range is { } selected)
        {
            var lines = DiffSelection.OfRows(document, diff.Source.RowOf(selected.First), diff.Source.LastRowOf(selected.Last));
            if (lines.Count == 0)
            {
                return DiffStagingCapture.Refused("No added or removed line is selected");
            }

            var before = DiffSelection.ChangedRowsBefore(document, diff.Source.RowOf(selected.First));

            return new DiffStagingCapture(new DiffStagingRequest(target, lines, true), hunkIndex, string.Empty, before);
        }

        var hunkLines = DiffSelection.OfHunk(document, row);
        if (hunkIndex >= 0 && hunkLines.Count == 0)
        {
            return DiffStagingCapture.Refused("The change has no added or removed line");
        }

        return new DiffStagingCapture(new DiffStagingRequest(target, hunkLines, false), hunkIndex, string.Empty);
    }

    /// <summary>After the next reload of the same file the cursor goes to the hunk at <paramref name="hunkIndex"/> (the next one when that hunk was staged).</summary>
    public void MoveToHunkAfterReload(int hunkIndex)
    {
        _hunkAfterReload = hunkIndex;
        _changeAfterReload = null;
    }

    /// <summary>
    /// After the next reload of the same file the cursor goes to the changed row with number <paramref name="changeNumber"/>
    /// (the first change after a staged range, the last one when none follows), scrolling only when it is off screen.
    /// </summary>
    public void MoveToChangeAfterReload(int changeNumber)
    {
        _changeAfterReload = changeNumber;
        _hunkAfterReload = null;
    }

    /// <summary>After the next reload of the same file the cursor goes where <paramref name="move"/> says (a staged or discarded hunk or range).</summary>
    public void MoveAfterReload(DiffCursorMove move)
    {
        if (move.ChangesBefore >= 0)
        {
            MoveToChangeAfterReload(move.ChangesBefore);
        }
        else
        {
            MoveToHunkAfterReload(move.HunkIndex);
        }
    }

    public bool HandleTextKey(KeyEvent key)
    {
        var diff = _activeSession()?.WorkingDiff;

        return _actions.HandleTextKey(diff, diff?.Source.Document ?? DiffDocument.Empty, key);
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>Call when the shown tab changed: hooks the new tab's diff store and restores its cursor.</summary>
    public void OnTabSwitched(TerminalTabSession? previous, TerminalTabSession? current)
    {
        if (previous?.WorkingDiff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
            _actions.AbandonTypedSearch(old);
        }

        if (_hooked is not null)
        {
            _hooked.StatusRead -= Sync;
        }

        _hooked = current;
        _actions.IsRestoring = true;
        try
        {
            _diffList.Source = current?.WorkingDiff?.Source;
            if (current?.WorkingDiff is { } diff)
            {
                diff.Source.GroupButtons = () =>
                    WorkingTreePaneRenderer.StagingButtons(_host.Workspace.ActiveTab?.WorkingTree);
                diff.Store.Changed += OnStoreChanged;
                current.StatusRead += Sync;
                _actions.RestoreRemembered(diff);
            }
        }
        finally
        {
            _actions.IsRestoring = false;
        }

        Sync();
    }

    /// <summary>
    /// Asks the store for the diff of the file under the cursor of the Changes view (or a remark when there is none). Does
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
        if (RequestOf(tree) is not { } request)
        {
            diff.Store.ShowNothing(WorkingTreePaneRenderer.NoFileText);
            return;
        }

        diff.Store.Show(request, diff.Generation);
    }

    // The file under the cursor, or all the files under the folder there.
    private static WorkingDiffRequest? RequestOf(WorkingTreeState tree)
    {
        if (tree.Selection is not { } selection)
        {
            return null;
        }

        var side = selection.Section == WorkingTreeSection.Staged ? WorkingDiffSide.Staged : WorkingDiffSide.Unstaged;
        if (!selection.IsFolder)
        {
            return tree.SelectedEntry is { } entry ? new WorkingDiffRequest(side, entry) : null;
        }

        var entries = tree.SelectedFolderEntries;

        if (entries.Count == 0)
        {
            return null;
        }

        return new WorkingDiffRequest(side, entries[0], new WorkingDiffFolder(selection.Path, entries));
    }

    private void OnWorkspaceChanged()
    {
        Sync();
        ReleaseStaleZoom();
        if (_activeSession()?.WorkingDiff is { Search.Phase: DiffSearchPhase.Editing } diff
            && !WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
        {
            // The pane lost focus (a click) while the search was being typed: keys no longer reach the box, so end it.
            _actions.AbandonTypedSearch(diff);
        }
    }

    // Focus left the diff (Esc, a click on a list): the zoom ends, so returning to the diff does not zoom it again.
    private void ReleaseStaleZoom()
    {
        if (_activeSession()?.WorkingDiff is { IsZoomed: true } diff
            && !WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
        {
            diff.IsZoomed = false;
            _relayout();
        }
    }

    private DiffPaneModel ModelOf(WorkingDiffSession diff, bool isFocused) =>
        _actions.ModelOf(diff, diff.Store.Options, isFocused);

    private bool ExecuteInDiff(RepositoryState tab, WorkingDiffSession diff, ActionId action, int? count)
    {
        if (_actions.TryExecuteView(diff, action, count))
        {
            return true;
        }

        if (action == ActionIds.PaneZoom)
        {
            diff.IsZoomed = !diff.IsZoomed;
            _relayout();
        }
        else if (action == ActionIds.DiffLoadAnyway)
        {
            Notice = diff.Store.LoadAnyway() ? "Loading the whole diff..." : "Nothing was cut short";
        }
        else if (TryChangeOptions(diff, action))
        {
            // The diff loads again with the new options.
        }
        else if (action == ActionIds.NavigateBack)
        {
            if (diff.Search.IsOpen)
            {
                _actions.ApplySearch(diff, DiffSearch.Cancel());
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

    // Back to the list of the side the diff shows.
    private void Back(RepositoryState tab) =>
        _host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(WorkingPanes.ListOf(WorkingPanes.SectionOf(tab.View.FocusedPane)))));

    private bool TryChangeOptions(WorkingDiffSession diff, ActionId action)
    {
        if (!DiffListActions.TryResolveOptions(diff.Store.Options, action, out var changed))
        {
            return false;
        }

        Notice = diff.Store.SetOptions(changed) ? DiffOptionsLabel.Describe(changed) : "Context is already at its limit";
        return true;
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
            _actions.ForgetSearch(diff);
            var previous = diff.ShownRequest;
            var isSameFile = diff.Store.Request is { } request && request == previous;
            var cursor = _diffList.SelectedItem ?? 0;
            var top = _diffList.Viewport.Y;
            diff.Source.Update(document);
            diff.ShownRequest = diff.Store.Request;
            if (isSameFile)
            {
                // The same file read again (a refresh): the cursor stays where it was, or goes to the hunk that follows a staged one.
                Restore(diff, cursor, top);
                MoveToPendingHunk(diff);
            }
            else
            {
                var hasPendingMove = _hunkAfterReload is not null || _changeAfterReload is not null;
                if (hasPendingMove && IsSamePath(diff.Store.Request, previous))
                {
                    // Staging reads as another request while it loads: the view stays where it was instead of going to the top.
                    Restore(diff, cursor, top);
                }
                else if (diff.Source.Range is null)
                {
                    // A Visual range that survived (same rows) keeps its cursor too (#806).
                    _actions.Select(diff, 0, atTop: true);
                }

                // Staging changes the file's status letters, so it reads as another request while it loads: the jump waits for the same path.
                if (IsSamePath(diff.Store.Request, previous))
                {
                    MoveToPendingHunk(diff);
                }
                else
                {
                    _hunkAfterReload = null;
                    _changeAfterReload = null;
                }
            }
        }

        _diffList.SetNeedsDraw();
        _refreshChrome();
    }

    private static bool IsSamePath(WorkingDiffRequest? request, WorkingDiffRequest? other) =>
        request is not null && other is not null && request.Side == other.Side
        && request.Entry.Path == other.Entry.Path
        && request.Folder?.Path == other.Folder?.Path;

    private void MoveToPendingHunk(WorkingDiffSession diff)
    {
        if (diff.Store.Status != DiffStatus.Loaded)
        {
            return;
        }

        if (_changeAfterReload is { } number)
        {
            _changeAfterReload = null;
            var row = DiffSelection.ChangedRowNumbered(diff.Source.Document, number);
            if (row >= 0)
            {
                _actions.Select(diff, row, atTop: false);
            }

            return;
        }

        if (_hunkAfterReload is not { } index)
        {
            return;
        }

        _hunkAfterReload = null;
        var anchors = diff.Source.Document.HunkRows;
        if (anchors.Count > 0)
        {
            _actions.Select(diff, anchors[Math.Min(index, anchors.Count - 1)], atTop: true);
        }
    }

    private void Restore(WorkingDiffSession diff, int cursor, int top)
    {
        var count = diff.Source.Count;
        if (count == 0)
        {
            return;
        }

        _actions.IsRestoring = true;
        try
        {
            _diffList.SelectedItem = Math.Clamp(cursor, 0, count - 1);
            _diffList.Viewport = _diffList.Viewport with { Y = Math.Clamp(top, 0, Math.Max(0, count - _diffList.Viewport.Height)) };
        }
        finally
        {
            _actions.IsRestoring = false;
        }

        _actions.RememberCursor();
    }
}
