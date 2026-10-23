using Gitunia.Core.Graph;
using Gitunia.Core.IssueTracker;
using Gitunia.Core.Search.Models;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Tabs;

/// <summary>
/// The runtime side of one tab that the immutable <see cref="RepositoryState"/> does not hold: the streamed graph rows,
/// the list source drawing them and the load in flight. Touched from the UI thread only.
/// </summary>
public sealed class TabSession : IDisposable
{
    public TabSession(
        TabId tabId,
        GraphGlyphSet glyphs,
        CommitDetailsStore details,
        DiffStore? diffStore = null,
        WorkingDiffStore? workingDiffStore = null,
        FileTreeSession? tree = null,
        FileContentStore? contentStore = null)
    {
        Tree = tree;
        TabId = tabId;
        Details = details;
        Diff = diffStore is null ? null : new DiffSession(diffStore, glyphs, contentStore);
        WorkingDiff = workingDiffStore is null ? null : new WorkingDiffSession(workingDiffStore, glyphs);
        DetailSource = new DetailListSource(glyphs);
        Graph = new CommitGraph();
        Source = new GraphListSource(Graph, glyphs);
    }

    /// <summary>Raised after rows were appended or the load state changed.</summary>
    public event Action? Changed;

    /// <summary>Raised just before <see cref="ReplaceGraph"/> changes the rows, so the view can note its selection and scroll.</summary>
    public event Action? GraphReplacing;

    /// <summary>Raised after <see cref="ReplaceGraph"/> changed the rows, so the view can put its selection and scroll back.</summary>
    public event Action? GraphReplaced;

    /// <summary>Raised when the initial load ended, successfully or not.</summary>
    public event Action? LoadFinished;

    /// <summary>Raised by <see cref="RequestExpand"/> when the rest of a cut log should load.</summary>
    public event Action? ExpandRequested;

    /// <summary>Raised when a refresh read the status again and found it unchanged, so a file the status lists may still have changed.</summary>
    public event Action? StatusRead;

    /// <summary>Raised after <see cref="SetIssueLinks"/> changed the links, so the detail pane can show them.</summary>
    public event Action? IssueLinksChanged;

    public TabId TabId { get; }

    /// <summary>The rules of the repository's tracker file; none until read, and when the file is missing.</summary>
    public IssueTrackerLinks IssueLinks { get; private set; } = IssueTrackerLinks.None;

    /// <summary>Replaces the tracker rules (read when the tab loads and on a manual refresh).</summary>
    public void SetIssueLinks(IssueTrackerLinks links)
    {
        IssueLinks = links;
        IssueLinksChanged?.Invoke();
    }

    /// <summary>Counts graph replacements; lets the search notice that its matches are stale.</summary>
    public int GraphVersion { get; private set; }

    /// <summary>The <see cref="GraphVersion"/> the search last scanned.</summary>
    public int SearchGraphVersion { get; set; }

    public CommitGraph Graph { get; }

    public GraphListSource Source { get; }

    /// <summary>The tab's graph search (the typed query, its matches and scan progress).</summary>
    public GraphSearchState Search { get; set; } = GraphSearchState.Initial;

    /// <summary>The details of the commit selected in the graph, loaded on demand.</summary>
    public CommitDetailsStore Details { get; }

    /// <summary>The tab's Files tab (tree store, opened directories, filter); null in sessions built without a tree
    /// store (tests).</summary>
    public FileTreeSession? Tree { get; }

    /// <summary>The tab's diff pane; null in sessions built without a diff store (tests).</summary>
    public DiffSession? Diff { get; }

    /// <summary>The tab's working-tree diff; null in sessions built without a working diff store (tests).</summary>
    public WorkingDiffSession? WorkingDiff { get; }

    /// <summary>The list source of the detail pane.</summary>
    public DetailListSource DetailSource { get; }

    /// <summary>The folders of the Changes tab's tree style that are closed (paths from the repository root); kept for
    /// the tab's lifetime, not saved with the session.</summary>
    public HashSet<string> CollapsedFolders { get; } = new(StringComparer.Ordinal);

    /// <summary>The detail row last selected on each bottom tab for the commit shown; cleared when another commit is selected.</summary>
    public DetailCursorMemory DetailCursor { get; } = new();

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>The tab has not been activated since the session was restored, so its load has not started.</summary>
    public bool IsPending { get; private set; } = true;

    /// <summary>The log is still streaming in.</summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>The commits in the whole log when only its newest rows are loaded, null when all are.</summary>
    public int? TruncatedTotal { get; private set; }

    /// <summary>The last load of the rest of a cut log failed; see <see cref="RequestExpand"/>.</summary>
    public bool ExpansionFailed { get; private set; }

    /// <summary>The loaded rows are the newest part of a longer log.</summary>
    public bool IsTruncated => TruncatedTotal is not null;

    /// <summary>Every commit of the log is loaded: nothing streams and nothing was cut.</summary>
    public bool IsGraphComplete => !IsLoading && !IsTruncated;

    /// <summary>Why loading stopped early, empty when it did not.</summary>
    public string LoadError { get; private set; } = string.Empty;

    /// <summary>A refresh read the status and it did not change: the working-tree diff of the shown file is read again.</summary>
    public void NotifyStatusRead()
    {
        WorkingDiff?.Invalidate();
        StatusRead?.Invoke();
    }

    public void MarkStarted()
    {
        IsPending = false;
        Changed?.Invoke();
    }

    public void Append(IReadOnlyList<GraphEntry> batch)
    {
        Graph.AddRange(batch);
        Source.NotifyAppended();
        Changed?.Invoke();
    }

    /// <summary>Marks the loaded rows as the newest part of a log with <paramref name="total"/> commits.</summary>
    public void Truncate(int total)
    {
        TruncatedTotal = total;
        RefreshCutMarker();
    }

    // The marker shows while the log is cut and nothing loads; the item count changes with it.
    private void RefreshCutMarker()
    {
        var shown = IsTruncated && !IsLoading;
        if (shown != Source.ShowsCutMarker)
        {
            Source.ShowsCutMarker = shown;
            Source.NotifyAppended();
        }
    }

    /// <summary>
    /// Asks for the rest of a cut log; does nothing when the log is whole, the rest is already loading or nothing
    /// answers (a session without an effect runner). After a failed load only a user's own action tries again, so a
    /// restore or a search scan cannot loop on a broken read.
    /// </summary>
    /// <param name="isUserAction">The user asked (a key or a jump): retry even after a failure.</param>
    public void RequestExpand(bool isUserAction = false)
    {
        if (isUserAction)
        {
            ExpansionFailed = false;
        }

        if (!IsTruncated || IsLoading || ExpansionFailed || ExpandRequested is null)
        {
            return;
        }

        IsLoading = true;
        RefreshCutMarker();
        Changed?.Invoke();
        ExpandRequested.Invoke();
    }

    /// <summary>Loading the rest of a cut log stopped early; the log stays cut and can be requested again.</summary>
    public void EndExpansion()
    {
        ExpansionFailed = true;
        IsLoading = false;
        RefreshCutMarker();
        Changed?.Invoke();
        LoadFinished?.Invoke();
    }

    /// <summary>The rest of a cut log has been appended: the graph is whole now.</summary>
    public void CompleteExpansion()
    {
        TruncatedTotal = null;
        Complete();
    }

    /// <summary>Swaps in a refreshed log in one step; <paramref name="truncatedTotal"/> is set if it was cut.</summary>
    public void ReplaceGraph(IReadOnlyList<GraphEntry> entries, int? truncatedTotal = null)
    {
        GraphReplacing?.Invoke();
        LoadError = string.Empty;
        TruncatedTotal = truncatedTotal;
        ExpansionFailed = false;
        IsLoading = false;
        Graph.Replace(entries);
        GraphVersion++;
        Source.ShowsCutMarker = IsTruncated;
        Source.NotifyAppended();
        GraphReplaced?.Invoke();
        Changed?.Invoke();
    }

    public void Complete()
    {
        IsLoading = false;
        RefreshCutMarker();
        Changed?.Invoke();
        LoadFinished?.Invoke();
    }

    public void Fail(string message)
    {
        IsLoading = false;
        LoadError = message;
        Changed?.Invoke();
        LoadFinished?.Invoke();
    }

    public void Dispose()
    {
        Details.Dispose();
        Tree?.Dispose();
        Diff?.Dispose();
        WorkingDiff?.Dispose();
        Cancellation.Cancel();
        Cancellation.Dispose();
    }
}
