using Gitunia.App;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Trees;
using Gitunia.Core.Diff;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// The terminal side of the diff pane's content: it brings the pane's list in line with the tab's
/// <see cref="DiffPaneState"/> (the loaded diff cut to the file or folder it asks for, the content of a file, the
/// placeholder of the Files tab) and with the stores' events, and tells the layout of the pane. The decisions are the
/// reducer's (<see cref="DiffPaneReducer"/>); this class only draws what the state says.
/// </summary>
public sealed class DiffPaneView
{
    private const string SideBySideHint = " (zoom with <space>z)";

    private readonly Dispatcher _host;
    private readonly IActiveSessions _sessions;
    private readonly ListView _diffList;
    private readonly IPaneChrome _chrome;

    /// <param name="host">The state.</param>
    /// <param name="sessions">The shown tab's runtime session.</param>
    /// <param name="lists">The lists of the panes.</param>
    /// <param name="chrome">The screen the pane sits in.</param>
    /// <param name="clipboard">Copies diff text.</param>
    public DiffPaneView(
        Dispatcher host,
        IActiveSessions sessions,
        IPaneLists lists,
        IPaneChrome chrome,
        ClipboardCopier clipboard)
    {
        _host = host;
        _sessions = sessions;
        _diffList = lists.ListOf(PaneKind.Diff);
        _chrome = chrome;
        Actions = new DiffListActions(_diffList, host, clipboard, () => sessions.ActiveSession()?.Diff, SideBySideHint);
        host.Changed += Reconcile;
    }

    /// <summary>The cursor, scroll, wrap, search and copy behaviour the pane shares with the Changes view diff.</summary>
    public DiffListActions Actions { get; }

    /// <summary>The active tab's diff pane shows a file's content, not a diff.</summary>
    public bool IsShowingContent => ActiveState().IsShowingContent;

    /// <summary>The terminal shows the diff beside the file list (the Changes and Files tabs on a roomy screen).</summary>
    public bool ShowsWithChanges() => _host.Workspace.ActiveTab is { } tab && PaneLayout.ShowsDiffWithChanges(
        _chrome.ScreenSize.Width,
        _chrome.ScreenSize.Height,
        tab.View.BottomTab);

    /// <summary>The Files tab is showing and the screen has room for its content beside the tree.</summary>
    public bool IsFilesBeside() =>
        _host.Workspace.ActiveTab?.View.BottomTab == BottomTab.Files && ShowsWithChanges();

    /// <summary>The pane is on screen: opened on demand, or shown by the Changes tab.</summary>
    public bool IsShown() => ActiveState().IsOpen || ShowsWithChanges();

    /// <summary>The panes' arrangement for the active tab; a zoom that does not match the focused pane is not part of it.</summary>
    public PaneArrangement Arrangement()
    {
        if (_sessions.ActiveSession()?.Diff is null)
        {
            return PaneArrangement.Normal with { Sizes = SizesOfActiveTab() };
        }

        var diff = ActiveState();
        var focused = _host.Workspace.ActiveTab?.View.FocusedPane;
        var shows = ShowsWithChanges();
        var together = DiffPaneRules.KeepsZoomTogether(diff.Zoomed, focused, shows);
        var zoomed = DiffPaneRules.EffectiveZoom(diff, focused, shows);

        return new PaneArrangement(DiffPaneRules.IsShown(diff, shows), zoomed, SizesOfActiveTab(), together);
    }

    /// <summary>The pane sizes saved in the active tab's view state.</summary>
    public PaneSizes SizesOfActiveTab() => _host.Workspace.ActiveTab?.View is { } view
        ? new PaneSizes(view.SidebarWidth, view.BottomHeight, view.DetailWidth)
        : PaneSizes.Automatic;

    /// <summary>The diff pane's title row for the active tab.</summary>
    public StyledLine TitleLine(int width, GraphGlyphSet glyphs, ChipKeys? keys = null, bool isRepeating = false) =>
        DiffPaneRenderer.RenderTitle(
            ModelOf(_sessions.ActiveSession()?.Diff, _diffList.HasFocus) with { IsRepeating = isRepeating },
            width,
            glyphs,
            keys);

    /// <summary>Call when the shown tab changed: hooks the new tab's stores and restores its cursor.</summary>
    public void OnTabSwitched(TerminalTabSession? previous, TerminalTabSession? current)
    {
        if (previous?.Diff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
            if (old.ContentStore is { } oldContent)
            {
                oldContent.Changed -= OnContentChanged;
            }

            old.Store.ReleaseCache();
            Actions.AbandonTypedSearch(old);
        }

        Actions.IsRestoring = true;
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

                Actions.RestoreRemembered(diff);
            }
        }
        finally
        {
            Actions.IsRestoring = false;
        }

        if (current?.Diff is { } shown)
        {
            Reconcile();
            ShowCurrent(shown);
        }

        _chrome.Relayout();
    }

    /// <summary>Shows what the state asks for: the content of the open file, or the loaded diff.</summary>
    public void ShowCurrent()
    {
        if (_sessions.ActiveSession()?.Diff is { } diff)
        {
            ShowCurrent(diff);
        }
    }

    /// <summary>
    /// Takes the pending file or folder from the loaded diff (the state remembers which file the pane shows) and puts the
    /// document of that file in the list. Does nothing beside the Files tab or while the pane shows a file's content.
    /// </summary>
    public void Refresh()
    {
        if (_sessions.ActiveSession()?.Diff is not { } diff || _host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        var state = tab.View.DiffPane;
        if (state.ShowsFilesPane || IsFilesBeside())
        {
            return;
        }

        var full = diff.Store.Document;
        var isLoaded = diff.Store.Status == DiffStatus.Loaded;
        if (DiffShown.Refresh(state, full, isLoaded) != state)
        {
            _host.Dispatch(new DiffRefreshed(tab.Id, full, isLoaded));
        }

        var cut = DiffShown.Cut(ActiveState(), full, isLoaded);
        if (!ReferenceEquals(diff.ShownFull, full) || diff.ShownFile != cut.File || diff.BuiltFolderPath != cut.BuiltFolder)
        {
            // The pane holds one file only, the one under the file list's cursor, or the files of the folder there.
            diff.ShownFull = full;
            diff.ShownFile = cut.File;
            diff.BuiltFolderPath = cut.BuiltFolder;
            diff.ShownDocument = DiffShown.DocumentOf(full, cut);
        }

        Show(diff, diff.ShownDocument);
    }

    /// <summary>Shows the content of the open file (its text, or a note while it loads or when it cannot be shown).</summary>
    public void ShowContent()
    {
        if (_sessions.ActiveSession()?.Diff is { ContentStore: { } store } diff)
        {
            ShowContent(diff, store);
        }
    }

    private DiffPaneState ActiveState() => _host.Workspace.ActiveTab?.View.DiffPane ?? DiffPaneState.Empty;

    private DiffPaneModel ModelOf(DiffSession? diff, bool isFocused)
    {
        if (diff is null)
        {
            return new DiffPaneModel(DiffDocument.Empty, 0, 0, isFocused);
        }

        var isFolder = diff.BuiltFolderPath is not null;
        var fileNumber = isFolder ? 0 : diff.ShownFile + 1;
        var fileCount = diff.ShownFull?.FilePaths.Count ?? 0;
        if (isFolder)
        {
            fileCount = diff.ShownDocument.FilePaths.Count;
        }

        return Actions.ModelOf(diff, diff.Store.Options, isFocused) with
        {
            IsFolder = isFolder,
            FileNumber = fileNumber,
            FileCount = fileCount,
        };
    }

    // The state changed (a command, or the shown tab): bring the list in line with what changed in it.
    private void Reconcile()
    {
        if (_sessions.ActiveSession()?.Diff is not { } diff || _host.Workspace.ActiveTab is not { } tab)
        {
            return;
        }

        var state = tab.View.DiffPane;
        var applied = diff.Applied;
        if (state == applied)
        {
            return;
        }

        diff.Applied = state;
        if (state.Placeholder != DiffPlaceholder.None)
        {
            ShowPlaceholder(diff, state.Placeholder);
        }
        else if (!state.IsShowingContent && applied.ShowsFilesPane)
        {
            CloseContent(diff);
        }
        else if (!state.ShowsFilesPane)
        {
            EmptyIfCleared(diff);
        }

        if (state.IsOpen != applied.IsOpen || state.Zoomed != applied.Zoomed || state.ShowsFilesPane != applied.ShowsFilesPane)
        {
            _chrome.Relayout();
        }
    }

    private void ShowCurrent(DiffSession diff)
    {
        var state = ActiveState();
        if (state.IsShowingContent && diff.ContentStore is { } store)
        {
            ShowContent(diff, store);
        }
        else if (!state.ShowsFilesPane && IsShown())
        {
            // The load may have finished while the tab was in the background.
            Refresh();
        }
    }

    private void OnStoreChanged() => Refresh();

    private void OnContentChanged()
    {
        if (_sessions.ActiveSession()?.Diff is { ContentStore: { } store } diff
            && ActiveState() is { IsShowingContent: true } state
            && store.Current.Path == state.ContentPath
            && store.Current.Commit == state.ContentCommit)
        {
            ShowContent(diff, store);
        }
    }

    private void ShowContent(DiffSession diff, FileContentStore store)
    {
        if (!ReferenceEquals(diff.Source.Document, store.Current.Document))
        {
            Show(diff, store.Current.Document);
        }
        else
        {
            _diffList.SetNeedsDraw();
            _chrome.RefreshChrome();
        }
    }

    // The note of the Files tab's pane while no file is under the tree's cursor.
    private void ShowPlaceholder(DiffSession diff, DiffPlaceholder placeholder)
    {
        var text = placeholder == DiffPlaceholder.Submodule
            ? FileContentDocument.SubmodulePlaceholder
            : FileContentDocument.FilePlaceholder;
        if (diff.Source.Document.Rows is [{ } only] && only.Text == text)
        {
            return;
        }

        Show(diff, FileContentDocument.Placeholder(text));
    }

    // Leaves the file content without touching the loaded diff: the pane is empty until the diff shows again.
    private void CloseContent(DiffSession diff)
    {
        Empty(diff);
        if (ShowsWithChanges())
        {
            Refresh();
        }

        _chrome.Relayout();
        _chrome.RefreshChrome();
    }

    // A closed pane forgot its diff: its list is empty too.
    private void EmptyIfCleared(DiffSession diff)
    {
        if (diff.Store.Status == DiffStatus.None && !IsShown() && !ReferenceEquals(diff.Source.Document, DiffDocument.Empty))
        {
            Empty(diff);
        }
    }

    private void Empty(DiffSession diff)
    {
        Actions.ForgetSearch(diff);
        diff.Source.Highlight = null;
        diff.ShownFull = null;
        diff.ShownDocument = DiffDocument.Empty;
        diff.Source.Update(DiffDocument.Empty);
    }

    // Puts a document in the list. A new document ends the search over the old one and puts the cursor on its first row,
    // unless a Visual range survived (same rows), which keeps its cursor too (#806).
    private void Show(DiffSession diff, DiffDocument document)
    {
        if (!ReferenceEquals(diff.Source.Document, document))
        {
            Actions.ForgetSearch(diff);
            diff.Source.Update(document);
            if (diff.Source.Range is null)
            {
                Actions.Select(diff, 0, atTop: true);
            }
        }

        _diffList.SetNeedsDraw();
        _chrome.RefreshChrome();
    }
}
