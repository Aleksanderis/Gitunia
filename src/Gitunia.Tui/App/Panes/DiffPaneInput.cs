using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Trees;
using Gitunia.Core;
using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
using Gitunia.Core.FileTree;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The keys of the diff pane of the active tab (on the Files tab: the file content beside the tree, following its
/// cursor). The Changes tab shows the pane beside the file list where the screen has room, and follows its cursor;
/// elsewhere <c>Enter</c> opens it on demand. It reads what the lists and stores know (the cursor rows, the selected
/// commit, the loaded diff), turns the keys into <see cref="DiffPaneCommand"/>s and lets <see cref="DiffPaneReducer"/>
/// decide; the view only draws the result (<see cref="DiffPaneView"/>). Scrolling, searching and copying run in
/// <see cref="DiffListActions"/>. It is also the diff's search prompt (the box in Insert mode), so it plugs into the
/// same prompt list as the graph search.
/// </summary>
public sealed class DiffPaneInput : IActionHandler, ITextKeyHandler, IActionNotice, IPromptDisplay
{
    private readonly Dispatcher _host;
    private readonly IActiveSessions _sessions;
    private readonly ListView _detailList;
    private readonly DiffPaneView _view;
    private readonly DiffListActions _actions;

    /// <param name="host">The state and the commands.</param>
    /// <param name="sessions">The shown tab's runtime session.</param>
    /// <param name="lists">The lists of the panes: the detail list is where a file is picked.</param>
    /// <param name="view">Draws the pane.</param>
    public DiffPaneInput(Dispatcher host, IActiveSessions sessions, IPaneLists lists, DiffPaneView view)
    {
        _host = host;
        _sessions = sessions;
        _detailList = lists.ListOf(PaneKind.Detail);
        _view = view;
        _actions = view.Actions;
        host.Changed += OnWorkspaceChanged;
    }

    public string Notice
    {
        get => _actions.Notice;
        private set => _actions.Notice = value;
    }

    public string StatusLine => _actions.StatusLine(_sessions.ActiveSession()?.Diff);

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        _actions.SyncWidth();
        FollowSelection();
        if (_sessions.ActiveSession()?.Diff is not { } diff || _host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        var focused = tab.View.FocusedPane;
        if (action == ActionIds.PaneZoom)
        {
            _host.Dispatch(new DiffZoomed(tab.Id));

            return true;
        }

        if (focused == PaneKind.Detail && _view.ShowsWithChanges() && _actions.TryScroll(diff, action, count))
        {
            return true;
        }

        if (action == ActionIds.DetailOpenDiff && focused == PaneKind.Detail)
        {
            OpenSelectedFile(tab);

            return true;
        }

        return focused == PaneKind.Diff && ExecuteInDiff(tab, diff, action, count);
    }

    /// <summary>The quick-jump target of the active tab's diff, null while it has none.</summary>
    public IJumpTarget? JumpTarget() =>
        _sessions.ActiveSession()?.Diff is { } diff ? _actions.JumpTarget(PaneKind.Diff, diff) : null;

    public bool HandleTextKey(KeyEvent key)
    {
        var diff = _sessions.ActiveSession()?.Diff;

        return _actions.HandleTextKey(diff, diff?.Source.Document ?? DiffDocument.Empty, key);
    }

    /// <summary>
    /// Makes an open diff follow the graph selection: when the selected commit's details have arrived and differ from the
    /// commit the diff shows, the diff loads that commit and keeps the file under the cursor when the new commit has it.
    /// Beside the Files tab the pane follows the tree's cursor instead. Does nothing while the diff is closed or the
    /// details are still loading.
    /// </summary>
    public void FollowSelection()
    {
        if (_sessions.ActiveSession() is not { Diff: { } diff } session || _host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        var target = TargetOf(session);
        var facts = new DiffFollowFacts(
            session.Details.Current.Commit,
            target,
            target is { } shown && diff.Store.IsShowing(shown.Commit, shown.Parent),
            diff.Store.Status == DiffStatus.Loaded,
            CurrentFilePath(diff),
            CursorOf(session),
            ContentOf(session));

        DispatchIfEffective(new DiffFollowed(tab.Id, facts));
    }

    /// <summary>
    /// Opens the content of a file of the Files tab read-only in the diff pane's place and gives the pane focus. The
    /// blob is read off the UI thread (size cap and binary notice: <see cref="FileContentStore"/>). Says why in the
    /// status bar when nothing was opened.
    /// </summary>
    public void OpenFileContent(FileTreeRow row)
    {
        if (_host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        var session = _sessions.ActiveSession();
        var treeCommit = session is { Diff.ContentStore: not null, Tree: { } tree } ? tree.Store.Current.Commit : null;
        _host.Dispatch(new DiffContentOpened(tab.Id, row, treeCommit));
        _view.ShowContent();
    }


    /// <summary>The terminal changed size: loads the diff that now has room.</summary>
    public void OnScreenResized() => FollowSelection();

    /// <summary>
    /// The file list's cursor moved: the diff beside it shows that file at once (the whole commit is loaded already, so this
    /// only scrolls; while the commit is still loading the file is remembered and shown when it arrives).
    /// </summary>
    public void OnDetailCursorMoved()
    {
        if (_sessions.ActiveSession() is not { Diff: { } diff } session || _host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        var cursor = CursorOf(session);
        DispatchIfEffective(new DiffCursorMoved(tab.Id, cursor, ContentOf(session)));
        if (!_view.IsFilesBeside()
            && _view.ShowsWithChanges()
            && cursor.IsOnItem
            && diff.Store.Status == DiffStatus.Loaded
            && session.Details.Current is { Commit: { } commit, Details: { } shown }
            && diff.Store.Commit == DiffCommit(shown, commit))
        {
            _view.Refresh();
        }
    }

    // The Files tab was selected, or the tree changed: the pane shows what is under the tree's cursor. A search box that
    // lost focus (a click) can no longer be typed into, so it ends.
    private void OnWorkspaceChanged()
    {
        if (_sessions.ActiveSession() is not { Diff: { } diff } session || _host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        if (_view.IsFilesBeside() && !ContentOf(session).IsShownBy(tab.View.DiffPane))
        {
            DispatchIfEffective(new DiffCursorMoved(tab.Id, DiffCursor.None, ContentOf(session)));
        }

        if (diff.Search.Phase == DiffSearchPhase.Editing && tab.View.FocusedPane != PaneKind.Diff)
        {
            _actions.AbandonTypedSearch(diff);
        }
    }

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

        var options = diff.Store.Options;
        if (action == ActionIds.DiffLoadAnyway)
        {
            var isCut = diff.Store.IsCut && diff.Store.Status == DiffStatus.Loaded && diff.Store.Commit is not null;
            _host.Dispatch(new DiffLoadAnywayRequested(tab.Id, isCut, CurrentFilePath(diff)));
        }
        else if (DiffOptionKeys.TryResolve(options, action, out _))
        {
            _host.Dispatch(new DiffOptionsRequested(tab.Id, action, options, CurrentFilePath(diff)));
        }
        else if (action == ActionIds.DiffNextFile || action == ActionIds.DiffPreviousFile)
        {
            MoveToFile(tab, diff, action == ActionIds.DiffNextFile, count ?? 1);
        }
        else if (action == ActionIds.NavigateBack)
        {
            Back(tab, diff);
        }
        else
        {
            return false;
        }

        return true;
    }

    private void Back(RepositoryState tab, DiffSession diff)
    {
        if (diff.Search.IsOpen)
        {
            _actions.ApplySearch(diff, DiffSearch.Cancel());
        }
        else
        {
            _host.Dispatch(new DiffClosed(tab.Id));
        }
    }

    private void OpenSelectedFile(RepositoryState tab)
    {
        if (_sessions.ActiveSession() is not { Diff: not null } session)
        {
            return;
        }

        var facts = new DiffOpenFacts(
            TargetOf(session),
            session.DetailSource.Content.Tab == BottomTab.Changes,
            CursorOf(session));
        _host.Dispatch(new DiffFileOpened(tab.Id, facts));

        // The same commit may be loaded already: only the cursor moves to the chosen file.
        _view.Refresh();
    }

    // ]f and [f pick the next or previous file of the commit; the pane then shows that file only. The file list's
    // cursor follows, so the list and the diff stay on the same file. Past the first or last file nothing moves.
    private void MoveToFile(RepositoryState tab, DiffSession diff, bool forward, int count)
    {
        if (_host.Workspace.ActiveTab?.View.DiffPane.ShowsFilesPane == true)
        {
            MoveToFileRow(forward, count);

            return;
        }

        if (diff.BuiltFolderPath is not null)
        {
            MoveToFileOfFolder(diff, forward, count);

            return;
        }

        var full = diff.ShownFull;
        var target = full is null ? -1 : DiffFileStep.Target(diff.ShownFile, full.FilePaths.Count, forward, count);
        if (full is null || target < 0)
        {
            Notice = forward ? "No next file" : "No previous file";

            return;
        }

        var path = full.FilePaths[target][0];
        SelectChangesRow(path);
        _host.Dispatch(new DiffFileChosen(tab.Id, path));
        _view.Refresh();
    }

    private void SelectChangesRow(string path)
    {
        if (_sessions.ActiveSession()?.DetailSource is not { Content: { Tab: BottomTab.Changes } content })
        {
            return;
        }

        for (var index = 0; index < content.Files.Count; index++)
        {
            if (content.Files[index].Path == path && content.RowOfFile(index) is >= 0 and var row)
            {
                _detailList.SelectedItem = row;
                _detailList.EnsureSelectedItemVisible();

                return;
            }
        }
    }

    // In the folder view the pane holds several files: ]f and [f move the cursor from file to file inside it.
    private void MoveToFileOfFolder(DiffSession diff, bool forward, int count)
    {
        var document = diff.ShownDocument;
        var row = _actions.CursorRow(diff);
        var current = row >= 0 && row < document.Rows.Count ? Math.Max(0, document.Rows[row].FileIndex) : 0;
        var target = DiffFileStep.Target(current, document.FileRows.Count, forward, count);
        if (target < 0)
        {
            Notice = forward ? "No next file" : "No previous file";

            return;
        }

        _actions.Select(diff, document.FileRows[target], atTop: true);
    }

    // ]f and [f in the file content pane: the tree's cursor moves to the next or previous file row, the pane follows.
    private void MoveToFileRow(bool forward, int count)
    {
        if (_sessions.ActiveSession() is not { } session || !_view.IsFilesBeside())
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
            if (rows[next].Kind is not (FileTreeRowKind.Directory or FileTreeRowKind.Submodule))
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
    }

    // Runs the command only when it changes something: the key handlers ask on every key, and a dispatch redraws.
    private void DispatchIfEffective(DiffPaneCommand command)
    {
        var update = AppReducer.Reduce(_host.State, command);
        if (!ReferenceEquals(update.State, _host.State) || !update.Effects.IsEmpty)
        {
            _host.Dispatch(command);
        }
    }

    private static DiffTarget? TargetOf(TerminalTabSession session)
    {
        if (session.Details.Current is not { Details: { } loaded, Commit: { } commit })
        {
            return null;
        }

        return new DiffTarget(
            DiffCommit(loaded, commit),
            ParentToCompare(loaded),
            loaded.ParentIndex > 0 || loaded.IsComparison,
            loaded.IsComparison);
    }

    // The parent the diff is read against: the first one, the one a merge was switched to, or the older commit of a
    // comparison.
    private static ObjectId? ParentToCompare(CommitDetails details) =>
        details.CompareOlder
        ?? (details.Parents.Count > details.ParentIndex ? details.Parents[details.ParentIndex] : null);

    // The commit the diff shows: the selected one, or the newer commit of a comparison.
    private static ObjectId DiffCommit(CommitDetails details, ObjectId selected) => details.CompareNewer ?? selected;

    private DiffCursor CursorOf(TerminalTabSession session)
    {
        if (session.DetailSource is not { Content: { Tab: BottomTab.Changes } content })
        {
            return DiffCursor.None;
        }

        var row = _detailList.SelectedItem ?? -1;
        var index = content.FileIndexAt(row);
        var file = index >= 0 && index < content.Files.Count ? content.Files[index].Path : null;

        return new DiffCursor(content.FolderAt(row), file);
    }

    private DiffContentTarget ContentOf(TerminalTabSession session)
    {
        var rows = session.DetailSource.Content is { Tab: BottomTab.Files } content ? content.Tree : [];
        var index = _detailList.SelectedItem ?? -1;
        var row = index >= 0 && index < rows.Count ? rows[index] : null;

        return DiffContentTarget.Of(row, session.Tree?.Store.Current.Commit, session.Details.Current.Commit);
    }

    private string? CurrentFilePath(DiffSession diff)
    {
        var document = diff.Source.Document;
        var row = _actions.CursorRow(diff);
        var file = row >= 0 && row < document.Rows.Count ? document.Rows[row].FileIndex : -1;

        return file >= 0 && file < document.FilePaths.Count ? document.FilePaths[file][0] : null;
    }
}
