using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Diff.Models;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;
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
public sealed partial class WorkingDiffController : IActionHandler, ITextKeyHandler, IActionNotice, IPromptDisplay
{
    private readonly Dispatcher _host;
    private readonly IActiveSessions _sessions;
    private readonly ListView _diffList;
    private readonly IPaneChrome _chrome;
    private readonly DiffListActions _actions;
    private TerminalTabSession? _hooked;
    private int? _hunkAfterReload;
    private int? _changeAfterReload;

    /// <param name="host">The workspace the focus commands go to.</param>
    /// <param name="sessions">The active tab's runtime session.</param>
    /// <param name="lists">The list of the diff box.</param>
    /// <param name="chrome">Redraws the frame titles and the status bar after the diff changed on its own (it finished loading); lays the panes out again after the zoom changed.</param>
    /// <param name="clipboard">Copies diff text.</param>
    public WorkingDiffController(
        Dispatcher host,
        IActiveSessions sessions,
        IPaneLists lists,
        IPaneChrome chrome,
        ClipboardCopier clipboard)
    {
        _host = host;
        _sessions = sessions;
        _diffList = lists.ListOf(PaneKind.WorkingUnstagedDiff);
        _chrome = chrome;
        _actions = new DiffListActions(
            _diffList,
            host,
            clipboard,
            new ActiveWorkingDiff(sessions),
            new DiffListTexts(string.Empty, "change"));
        host.Changed += OnWorkspaceChanged;
    }

    public string Notice
    {
        get => _actions.Notice;
        private set => _actions.Notice = value;
    }

    /// <summary>True while the diff of the shown tab fills the whole Changes view (the diff has focus and was zoomed).</summary>
    public bool IsZoomed =>
        _sessions.ActiveSession()?.WorkingDiff is { IsZoomed: true }
        && WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph);

    public string StatusLine => _actions.StatusLine(_sessions.ActiveSession()?.WorkingDiff);

    /// <summary>The title of the diff box: the side, the hunk the cursor is in and the file, plus the markers of the diff options.</summary>
    /// <param name="isRepeating">The repeat mode is on for the diff box: the title shows its marker.</param>
    public string TitleText(bool isRepeating = false)
    {
        var tab = _host.Workspace.ActiveTab;
        var model = default(DiffPaneModel);
        if (_sessions.ActiveSession()?.WorkingDiff is { } diff)
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
        if (_sessions.ActiveSession()?.WorkingDiff is not { } diff)
        {
            return null;
        }

        return ModelOf(diff, WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph));
    }

    /// <summary>The quick-jump target of the diff box of the shown tab, null while it has none.</summary>
    public IJumpTarget? JumpTarget()
    {
        if (_sessions.ActiveSession()?.WorkingDiff is { } diff
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
        if (_sessions.ActiveSession()?.WorkingDiff is { } shown
            && _host.Workspace.ActiveTab is { View.Mode: TabViewMode.WorkingTree } listTab
            && WorkingPanes.IsList(listTab.View.FocusedPane)
            && _actions.TryScroll(shown, action, count))
        {
            return true;
        }

        if (_sessions.ActiveSession()?.WorkingDiff is not { } diff
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

    public bool HandleTextKey(KeyEvent key)
    {
        var diff = _sessions.ActiveSession()?.WorkingDiff;

        return _actions.HandleTextKey(diff, diff?.Source.Document ?? DiffDocument.Empty, key);
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
        if (_sessions.ActiveSession()?.WorkingDiff is not { } diff
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
        if (_sessions.ActiveSession()?.WorkingDiff is { Search.Phase: DiffSearchPhase.Editing } diff
            && !WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
        {
            // The pane lost focus (a click) while the search was being typed: keys no longer reach the box, so end it.
            _actions.AbandonTypedSearch(diff);
        }
    }

    // Focus left the diff (Esc, a click on a list): the zoom ends, so returning to the diff does not zoom it again.
    private void ReleaseStaleZoom()
    {
        if (_sessions.ActiveSession()?.WorkingDiff is { IsZoomed: true } diff
            && !WorkingPanes.IsDiff(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph))
        {
            diff.IsZoomed = false;
            _chrome.Relayout();
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
            _chrome.Relayout();
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
        if (!DiffOptionKeys.TryResolve(diff.Store.Options, action, out var changed))
        {
            return false;
        }

        Notice = diff.Store.SetOptions(changed) ? DiffOptionKeys.Describe(changed) : "Context is already at its limit";
        return true;
    }

    private void OnStoreChanged()
    {
        if (_sessions.ActiveSession()?.WorkingDiff is { } diff)
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
        _chrome.RefreshChrome();
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
