using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.DiffPane;

/// <summary>
/// The diff pane as pure transitions: when it opens, closes and zooms, which commit's diff it loads, which file or folder
/// it shows and which file's content it shows beside the Files tab. The state is the tab's <see cref="DiffPaneState"/>;
/// reading git and cutting the loaded diff into the list are left to the effect handler and the terminal layer.
/// </summary>
public static class DiffPaneReducer
{
    /// <summary>The notice for Enter before the commit details have arrived.</summary>
    public const string DetailsLoadingNotice = "The commit details are still loading";

    /// <summary>The notice for Enter on a tab that lists no files.</summary>
    public const string WrongTabNotice = "Open the Changes tab (gc) to pick a file";

    /// <summary>The notice for Enter on a row that is no file or folder.</summary>
    public const string NoFileNotice = "Select a file or folder to see its diff";

    /// <summary>The notice for Enter on a file of a tree that has not arrived.</summary>
    public const string TreeLoadingNotice = "The file tree is still loading";

    /// <summary>The notice for Enter on a submodule.</summary>
    public const string SubmoduleNotice = "A submodule has no content to show";

    /// <summary>The notice for a diff option key while the pane shows a file's content.</summary>
    public const string ContentHasNoOptionsNotice = "The file content has no diff options";

    /// <summary>The notice for more context at its limit.</summary>
    public const string ContextLimitNotice = "Context is already at its limit";

    /// <summary>The notice when nothing was cut short.</summary>
    public const string NothingCutNotice = "Nothing was cut short";

    /// <summary>The notice when the whole diff loads.</summary>
    public const string LoadingWholeNotice = "Loading the whole diff...";

    /// <summary>The notice for zooming the sidebar.</summary>
    public const string SidebarZoomNotice = "The sidebar cannot be zoomed";

    /// <summary>Long enough that a cursor held down the tree reads only where it rests, short enough not to be felt.</summary>
    public static readonly TimeSpan ContentFollowDelay = TimeSpan.FromMilliseconds(40);

    /// <summary>Applies a command; a command for a tab that is not open any more changes nothing.</summary>
    public static AppUpdate Reduce(AppState state, DiffPaneCommand command)
    {
        if (state.Workspace.FindTab(command.TabId) is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var pane = new Pane(state, tab, DiffPaneRules.ShowsWithChanges(state.Shell.Screen, tab.View.BottomTab));

        return command switch
        {
            DiffFollowed followed => Follow(pane, followed.Facts),
            DiffFileOpened opened => OpenFile(pane, opened.Facts),
            DiffContentOpened content => OpenContent(pane, content),
            DiffCursorMoved moved => MoveCursor(pane, moved),
            DiffRefreshed refreshed => Set(pane, DiffShown.Refresh(pane.Diff, refreshed.Full, refreshed.IsLoaded)),
            DiffOptionsRequested options => ChangeOptions(pane, options),
            DiffLoadAnywayRequested anyway => LoadAnyway(pane, anyway),
            DiffFileChosen chosen => Set(pane, pane.Diff with { PendingFile = chosen.Path, PendingFolder = null }),
            DiffClosed => Close(pane),
            DiffZoomed => Zoom(pane),
            DiffPaneReset => Set(pane, DiffPaneState.Empty),
            _ => throw new UnhandledCommandException(command),
        };
    }

    /// <summary>
    /// What follows every command for the shown tab: a zoom that does not match the focused pane is forgotten, the file
    /// content of the Files tab is dropped on another tab, and a focus on a diff that is no longer on screen goes back to
    /// the file list. Does nothing until the screen size is known.
    /// </summary>
    /// <param name="state">The state after a command.</param>
    public static AppUpdate Settle(AppState state)
    {
        if (state.Shell.Screen is null || state.Workspace.ActiveTab is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        var shows = DiffPaneRules.ShowsWithChanges(state.Shell.Screen, tab.View.BottomTab);
        var focused = tab.View.FocusedPane;
        var diff = tab.View.DiffPane;
        var settled = diff;
        List<AppEffect> effects = [];
        if (settled.Zoomed is { } zoomed
            && zoomed != focused
            && !DiffPaneRules.KeepsZoomTogether(zoomed, focused, shows))
        {
            settled = settled with { Zoomed = null };
        }

        if (settled.ShowsFilesPane && tab.View.BottomTab != BottomTab.Files)
        {
            // The file content belongs to the Files tab: another tab shows its own pane.
            settled = CloseContent(settled, shows);
            effects.Add(new ClearFileContentEffect(tab.Id));
        }

        var update = new Pane(state, tab, shows).With(settled);
        if (focused == PaneKind.Diff && !DiffPaneRules.IsShown(settled, shows))
        {
            // The Changes tab was left (or the screen shrank) while the diff had focus: back to the file list.
            update = Focus(update, tab.Id, PaneKind.Detail);
        }

        return new AppUpdate(update.State, [.. effects, .. update.Effects]);
    }

    private static AppUpdate Follow(Pane pane, DiffFollowFacts facts)
    {
        var diff = pane.Diff;
        if (pane.FilesBeside)
        {
            return FollowContent(pane, facts.Content);
        }

        if (diff.IsShowingContent)
        {
            // A file of one commit's tree stays on screen only while that commit is selected.
            if (facts.GraphCommit is { } onGraph && onGraph != diff.ContentCommit)
            {
                return Set(pane, CloseContent(diff, pane.ShowsWithChanges), new ClearFileContentEffect(pane.Tab.Id));
            }

            return pane.Unchanged;
        }

        if (!DiffPaneRules.IsShown(diff, pane.ShowsWithChanges)
            || facts.Target is not { } target
            || facts.StoreShowsTarget)
        {
            return pane.Unchanged;
        }

        var next = diff;
        if (pane.ShowsWithChanges && facts.Cursor.IsOnItem)
        {
            // Beside the file list the diff always shows the file or folder under its cursor.
            next = WithPending(diff, facts.Cursor);
        }
        else if (facts.StoreLoaded)
        {
            // While another commit is still loading, the document has no files: keep the file remembered so far.
            next = KeepShown(diff, facts.CurrentFilePath);
        }

        return Set(pane, next, new LoadDiffEffect(pane.Tab.Id, target, !target.IsComparison));
    }

    private static AppUpdate FollowContent(Pane pane, DiffContentTarget target)
    {
        var diff = pane.Diff;
        if (target.IsShownBy(diff))
        {
            return pane.Unchanged;
        }

        if (target.File is { } row && target.Commit is { } commit)
        {
            var shown = diff.WithoutContent() with { ContentPath = row.Path, ContentCommit = commit };

            return Set(pane, shown, new LoadFileContentEffect(pane.Tab.Id, row, commit, ContentFollowDelay));
        }

        var placeholder = diff.WithoutContent() with { Placeholder = target.Placeholder };

        return Set(pane, placeholder, new ClearFileContentEffect(pane.Tab.Id));
    }

    private static AppUpdate OpenFile(Pane pane, DiffOpenFacts facts)
    {
        if (facts.Target is not { } target)
        {
            return Notice(pane, DetailsLoadingNotice);
        }

        if (!facts.IsChangesTab)
        {
            return Notice(pane, WrongTabNotice);
        }

        if (!facts.Cursor.IsOnItem)
        {
            return Notice(pane, NoFileNotice);
        }

        // Beside the file list the diff is part of the tab already; only narrow or short screens open it on demand.
        var diff = WithPending(pane.Diff, facts.Cursor) with { IsOpen = !pane.ShowsWithChanges };
        var zoomed = DiffPaneRules.ZoomAfterOpening(diff, pane.State.Shell.Screen, pane.ShowsWithChanges);
        diff = diff with { Zoomed = zoomed };
        var update = Set(pane, diff, new LoadDiffEffect(pane.Tab.Id, target, false));

        return Focus(update, pane.Tab.Id, PaneKind.Diff);
    }

    private static AppUpdate OpenContent(Pane pane, DiffContentOpened opened)
    {
        var row = opened.Row;
        if (opened.TreeCommit is not { } commit)
        {
            return Notice(pane, TreeLoadingNotice);
        }

        if (row.Kind == FileTree.FileTreeRowKind.Submodule || row.Blob is null)
        {
            return Notice(pane, SubmoduleNotice);
        }

        var diff = pane.Diff;
        if (diff.ContentPath == row.Path && diff.ContentCommit == commit && (diff.IsOpen || pane.FilesBeside))
        {
            // Already open: only take focus, keeping the place in the file.
            return Focus(pane.Unchanged, pane.Tab.Id, PaneKind.Diff);
        }

        var shown = diff.WithoutContent() with
        {
            ContentPath = row.Path,
            ContentCommit = commit,
            IsOpen = !pane.FilesBeside,
        };
        var zoomed = DiffPaneRules.ZoomAfterOpening(diff, pane.State.Shell.Screen, pane.ShowsWithChanges);
        shown = shown with { Zoomed = zoomed };
        var update = Set(pane, shown, new LoadFileContentEffect(pane.Tab.Id, row, commit, TimeSpan.Zero));

        return Focus(update, pane.Tab.Id, PaneKind.Diff);
    }

    private static AppUpdate MoveCursor(Pane pane, DiffCursorMoved moved)
    {
        if (pane.FilesBeside)
        {
            return FollowContent(pane, moved.Content);
        }

        // The whole commit is loaded already, so showing another file only scrolls.
        if (pane.ShowsWithChanges && moved.Cursor.IsOnItem)
        {
            return Set(pane, WithPending(pane.Diff, moved.Cursor));
        }

        return pane.Unchanged;
    }

    private static AppUpdate ChangeOptions(Pane pane, DiffOptionsRequested request)
    {
        if (pane.Diff.ShowsFilesPane)
        {
            return Notice(pane, ContentHasNoOptionsNotice);
        }

        if (!DiffOptionKeys.TryResolve(request.Current, request.Action, out var changed))
        {
            return pane.Unchanged;
        }

        if (changed == request.Current)
        {
            return Notice(pane, ContextLimitNotice);
        }

        // The diff loads again with the new options; the cursor returns to the same file.
        var next = KeepShown(pane.Diff, request.CurrentFile);
        var update = Set(pane, next, new SetDiffOptionsEffect(pane.Tab.Id, changed));

        return Add(update, Info(DiffOptionKeys.Describe(changed)));
    }

    private static AppUpdate LoadAnyway(Pane pane, DiffLoadAnywayRequested request)
    {
        if (pane.Diff.ShowsFilesPane)
        {
            return Notice(pane, ContentHasNoOptionsNotice);
        }

        if (!request.IsCut)
        {
            return Notice(pane, NothingCutNotice);
        }

        var update = Set(pane, KeepShown(pane.Diff, request.CurrentFile), new LoadWholeDiffEffect(pane.Tab.Id));

        return Add(update, Info(LoadingWholeNotice));
    }

    private static AppUpdate Close(Pane pane)
    {
        var tabId = pane.Tab.Id;
        if (pane.ShowsWithChanges)
        {
            // The diff belongs to the Changes tab and stays: going back only returns to the file list.
            return Focus(Set(pane, pane.Diff with { IsOpen = false }), tabId, PaneKind.Detail);
        }

        var closed = DiffPaneState.Empty;

        return Focus(Set(pane, closed, new ClearDiffEffect(tabId)), tabId, PaneKind.Detail);
    }

    private static AppUpdate Zoom(Pane pane)
    {
        var focused = pane.Tab.View.FocusedPane;
        if (!PaneZoom.CanZoom(focused))
        {
            return Notice(pane, SidebarZoomNotice);
        }

        var current = DiffPaneRules.EffectiveZoom(pane.Diff, focused, pane.ShowsWithChanges);

        return Set(pane, pane.Diff with { Zoomed = PaneZoom.Toggle(current, focused) });
    }

    // Leaves the file content without touching the loaded diff: the pane is empty until the diff shows again.
    private static DiffPaneState CloseContent(DiffPaneState diff, bool showsWithChanges) =>
        diff.WithoutContent() with { IsOpen = false, Zoomed = showsWithChanges ? diff.Zoomed : null };

    private static DiffPaneState WithPending(DiffPaneState diff, DiffCursor cursor)
    {
        if (cursor.FolderPath is { } folder)
        {
            return diff with { PendingFolder = folder, PendingFile = null };
        }

        return cursor.FilePath is { } path ? diff with { PendingFile = path, PendingFolder = null } : diff;
    }

    // Shows the same thing again after a reload: the folder when one is shown, else the file.
    private static DiffPaneState KeepShown(DiffPaneState diff, string? filePath)
    {
        if (diff.ShownFolder is { } folder)
        {
            return diff with { PendingFolder = folder, PendingFile = null };
        }

        return diff with { PendingFile = filePath, PendingFolder = null };
    }

    private static AppUpdate Set(Pane pane, DiffPaneState next, params AppEffect[] effects)
    {
        var update = pane.With(next);

        return effects.Length == 0 ? update : new AppUpdate(update.State, [.. update.Effects, .. effects]);
    }

    private static AppUpdate Focus(AppUpdate update, TabId tabId, PaneKind pane)
    {
        var focused = Focus(update.State, tabId, pane);

        return new AppUpdate(focused.State, [.. update.Effects, .. focused.Effects]);
    }

    private static AppUpdate Focus(AppState state, TabId tabId, PaneKind pane)
    {
        var update = WorkspaceReducer.Reduce(state.Workspace, new TabCommand(tabId, new FocusPaneCommand(pane)));

        return new AppUpdate(state with { Workspace = update.Workspace }, [.. update.Effects]);
    }

    private static AppUpdate Add(AppUpdate update, AppEffect effect) =>
        new(update.State, [.. update.Effects, effect]);

    private static AppUpdate Notice(Pane pane, string text) => new(pane.State, [Info(text)]);

    private static PostStatusEffect Info(string text) => new(text, StatusSeverity.Info);

    private readonly record struct Pane(AppState State, RepositoryState Tab, bool ShowsWithChanges)
    {
        public DiffPaneState Diff => Tab.View.DiffPane;

        public bool FilesBeside => Tab.View.BottomTab == BottomTab.Files && ShowsWithChanges;

        public AppUpdate Unchanged => new(State, []);

        public AppUpdate With(DiffPaneState next)
        {
            if (next == Diff)
            {
                return Unchanged;
            }

            var update = WorkspaceReducer.Reduce(State.Workspace, new TabCommand(Tab.Id, new SetDiffPaneCommand(next)));

            return new AppUpdate(State with { Workspace = update.Workspace }, [.. update.Effects]);
        }
    }
}
