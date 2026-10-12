using Gitunia.Core.Diff;
using Gitunia.Core.Keymap;
using Gitunia.Core.Lists;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Terminal.Gui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// Runs the diff pane of the active tab: opens a file's diff from the detail pane, scrolls and jumps through it, searches it
/// and zooms panes. It is also the diff's search prompt (the box in Insert mode), so it plugs into the same prompt list as the
/// graph search. Everything it changes about focus goes through workspace commands.
/// </summary>
public sealed class DiffPaneController : IPromptHandler
{
    private readonly WorkspaceHost host;
    private readonly Func<TabSession?> activeSession;
    private readonly ListView detailList;
    private readonly ListView diffList;
    private readonly Action<KeyMode> setMode;
    private readonly Func<int> screenHeight;
    private readonly Action relayout;
    private readonly Action refreshChrome;
    private bool isRestoring;

    /// <param name="host">The workspace the focus commands go to.</param>
    /// <param name="activeSession">The active tab's runtime session.</param>
    /// <param name="detailList">The detail pane's list, where a file is picked.</param>
    /// <param name="diffList">The diff pane's list.</param>
    /// <param name="setMode">Switches the input mode (Insert while the search box is open).</param>
    /// <param name="screenHeight">Terminal rows, to tell whether the diff fits next to the graph.</param>
    /// <param name="relayout">Asks the views to lay out again after the pane arrangement changed.</param>
    /// <param name="refreshChrome">Redraws the pane titles and the status bar after the diff changed on its own (it finished loading).</param>
    public DiffPaneController(
        WorkspaceHost host,
        Func<TabSession?> activeSession,
        ListView detailList,
        ListView diffList,
        Action<KeyMode> setMode,
        Func<int> screenHeight,
        Action relayout,
        Action refreshChrome)
    {
        this.host = host;
        this.activeSession = activeSession;
        this.detailList = detailList;
        this.diffList = diffList;
        this.setMode = setMode;
        this.screenHeight = screenHeight;
        this.relayout = relayout;
        this.refreshChrome = refreshChrome;
        diffList.ValueChanged += (_, _) => RememberCursor();
    }

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => activeSession()?.Diff is { Search.IsOpen: true } diff ? diff.Search.Describe(diffList.SelectedItem ?? 0) : string.Empty;

    /// <summary>The panes' arrangement for the active tab; a zoom that no longer matches the focused pane is dropped here.</summary>
    public PaneArrangement Arrangement()
    {
        if (activeSession()?.Diff is not { } diff)
        {
            return PaneArrangement.Normal;
        }

        if (diff.ZoomedPane != host.Workspace.ActiveTab?.View.FocusedPane)
        {
            diff.ZoomedPane = null;
        }

        return new PaneArrangement(diff.IsOpen, diff.ZoomedPane);
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (activeSession()?.Diff is not { } diff || host.Workspace.ActiveTab is not { } tab)
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

    public bool HandleTextKey(KeyEvent key)
    {
        if (activeSession()?.Diff is not { Search.Phase: DiffSearchPhase.Editing } diff)
        {
            return false;
        }

        if (PromptTextEditor.Apply(diff.Search.Text, key) is { } text && text != diff.Search.Text)
        {
            ApplySearch(diff, DiffSearch.Edit(diff.Search, text, diff.Store.Document));
        }

        return true;
    }

    public void OnGraphChanged()
    {
    }

    /// <summary>The diff pane's title row for the active tab.</summary>
    public StyledLine TitleLine(int width, GraphGlyphSet glyphs) =>
        DiffPaneRenderer.RenderTitle(ModelOf(activeSession()?.Diff, diffList.HasFocus), width, glyphs);

    /// <summary>Call when the shown tab changed: hooks the new tab's diff store and restores its cursor.</summary>
    public void OnTabSwitched(TabSession? previous, TabSession? current)
    {
        if (previous?.Diff is { } old)
        {
            old.Store.Changed -= OnStoreChanged;
        }

        isRestoring = true;
        try
        {
            diffList.Source = current?.Diff?.Source;
            if (current?.Diff is { } diff)
            {
                diff.Store.Changed += OnStoreChanged;
                if (diff.Source.Count > 0)
                {
                    diffList.SelectedItem = Math.Min(diff.SelectedRow, diff.Source.Count - 1);
                    diffList.Viewport = diffList.Viewport with { Y = diff.TopRow };
                }
            }
        }
        finally
        {
            isRestoring = false;
        }

        if (current?.Diff is { IsOpen: true } open)
        {
            // The load may have finished while the tab was in the background.
            Refresh(open);
        }

        relayout();
    }

    private DiffPaneModel ModelOf(DiffSession? diff, bool isFocused)
    {
        if (diff is null)
        {
            return new DiffPaneModel(DiffDocument.Empty, string.Empty, 0, 0, isFocused);
        }

        var hash = diff.Store.Commit is { } commit ? commit.ToString()[..7] : string.Empty;
        return new DiffPaneModel(diff.Source.Document, hash, diffList.SelectedItem ?? 0, diffList.Viewport.Y, isFocused, diff.Search.Highlight);
    }

    private bool ExecuteInDiff(RepoState tab, DiffSession diff, ActionId action, int? count)
    {
        if (diff.Search.Phase == DiffSearchPhase.Editing)
        {
            return ExecuteInSearchBox(diff, action);
        }

        var document = diff.Source.Document;
        var row = diffList.SelectedItem ?? 0;
        if (ListMotion.TryMove(action, count, row, document.Rows.Count, diffList.Viewport.Height, out var target))
        {
            Select(target, atTop: false);
        }
        else if (action == ActionIds.DiffNextHunk || action == ActionIds.DiffPreviousHunk)
        {
            Jump(document.HunkRows, row, action == ActionIds.DiffNextHunk, count, "hunk");
        }
        else if (action == ActionIds.DiffNextFile || action == ActionIds.DiffPreviousFile)
        {
            Jump(document.FileRows, row, action == ActionIds.DiffNextFile, count, "file");
        }
        else if (action == ActionIds.DiffSearchOpen)
        {
            ApplySearch(diff, DiffSearch.Open());
            setMode(KeyMode.Insert);
        }
        else if (action == ActionIds.DiffSearchNext || action == ActionIds.DiffSearchPrevious)
        {
            var update = DiffSearch.Step(diff.Search, action == ActionIds.DiffSearchNext, count ?? 1, row);
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
                Close(tab, diff);
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool ExecuteInSearchBox(DiffSession diff, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            var update = DiffSearch.Accept(diff.Search, diff.Source.Document, diffList.SelectedItem ?? 0);
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

        setMode(KeyMode.Normal);
        return true;
    }

    private void OpenSelectedFile(RepoState tab, DiffSession diff)
    {
        var details = activeSession()?.Details.Current;
        var source = activeSession()?.DetailSource;
        if (details?.Details is not { } loaded || details.Commit is not { } commit || source is null)
        {
            Notice = "The commit details are still loading";
            return;
        }

        var index = (detailList.SelectedItem ?? -1) - source.Content.FirstFileRow;
        if (index < 0 || index >= source.Content.Files.Count)
        {
            Notice = "Select a file to see its diff";
            return;
        }

        diff.IsOpen = true;
        diff.PendingFilePath = source.Content.Files[index].Path;
        diff.ZoomedPane = PaneLayout.CanOpenDiff(screenHeight()) ? null : PaneKind.Diff;
        var started = diff.Store.Load(commit, loaded.Parents.Count > 0 ? loaded.Parents[0] : null);
        relayout();
        host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Diff)));
        if (!started)
        {
            // The same commit is already loaded: only the cursor moves to the chosen file.
            Refresh(diff);
        }
    }

    private void Close(RepoState tab, DiffSession diff)
    {
        diff.IsOpen = false;
        diff.ZoomedPane = null;
        diff.PendingFilePath = null;
        diff.Search = DiffSearchState.Idle;
        diff.Source.Highlight = null;
        diff.Store.Clear();
        diff.Source.Update(DiffDocument.Empty);
        host.Dispatch(new TabCommand(tab.Id, new FocusPaneCommand(PaneKind.Detail)));
        relayout();
    }

    private void Zoom(DiffSession diff, PaneKind focused)
    {
        if (!PaneZoom.CanZoom(focused))
        {
            Notice = "The sidebar cannot be zoomed";
            return;
        }

        diff.ZoomedPane = PaneZoom.Toggle(Arrangement().Zoomed, focused);
        relayout();
    }

    private void Jump(IReadOnlyList<int> anchors, int row, bool forward, int? count, string what)
    {
        if (DiffNavigator.Step(anchors, row, forward, count ?? 1) is { } target)
        {
            Select(target, atTop: true);
        }
        else
        {
            Notice = forward ? $"No next {what}" : $"No previous {what}";
        }
    }

    private void ApplySearch(DiffSession diff, DiffSearchUpdate update)
    {
        diff.Search = update.State;
        diff.Source.Highlight = update.State.Highlight;
        if (update.RevealRow is { } row)
        {
            Select(row, atTop: false);
        }

        diffList.SetNeedsDraw();
    }

    private void OnStoreChanged()
    {
        if (activeSession()?.Diff is { } diff)
        {
            Refresh(diff);
        }
    }

    private void Refresh(DiffSession diff)
    {
        var document = diff.Store.Document;
        if (!ReferenceEquals(diff.Source.Document, document))
        {
            // A different diff: the old search no longer describes it.
            if (diff.Search.Phase == DiffSearchPhase.Editing)
            {
                setMode(KeyMode.Normal);
            }

            diff.Search = DiffSearchState.Idle;
            diff.Source.Highlight = null;
            diff.Source.Update(document);
            Select(0, atTop: true);
        }

        if (diff.Store.Status == DiffStatus.Loaded && diff.PendingFilePath is { } path)
        {
            diff.PendingFilePath = null;
            var file = document.FileIndexOf(path);
            Select(file >= 0 ? document.FileRows[file] : 0, atTop: true);
        }

        diffList.SetNeedsDraw();
        refreshChrome();
    }

    private void Select(int row, bool atTop)
    {
        var count = diffList.Source?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(row, 0, count - 1);
        diffList.SelectedItem = clamped;
        if (atTop)
        {
            diffList.Viewport = diffList.Viewport with { Y = Math.Min(clamped, Math.Max(0, count - diffList.Viewport.Height)) };
        }
        else
        {
            diffList.EnsureSelectedItemVisible();
        }
    }

    private void RememberCursor()
    {
        if (!isRestoring && activeSession()?.Diff is { } diff)
        {
            diff.SelectedRow = diffList.SelectedItem ?? 0;
            diff.TopRow = diffList.Viewport.Y;
        }
    }
}
