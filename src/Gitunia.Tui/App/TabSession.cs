using Gitunia.Core.Graph;
using Gitunia.Core.Search;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// The runtime side of one tab that the immutable <see cref="RepoState"/> does not hold: the streamed graph rows,
/// the list source drawing them and the load in flight. Touched from the UI thread only.
/// </summary>
public sealed class TabSession : IDisposable
{
    public TabSession(TabId tabId, GraphGlyphSet glyphs, CommitDetailsStore details, DiffStore? diffStore = null)
    {
        TabId = tabId;
        Details = details;
        Diff = diffStore is null ? null : new DiffSession(diffStore, glyphs);
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

    public TabId TabId { get; }

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

    /// <summary>The tab's diff pane; null in sessions built without a diff store (tests).</summary>
    public DiffSession? Diff { get; }

    /// <summary>The list source of the detail pane.</summary>
    public DetailListSource DetailSource { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>The tab has not been activated since the session was restored, so its load has not started.</summary>
    public bool IsPending { get; private set; } = true;

    /// <summary>The log is still streaming in.</summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>Why loading stopped early, empty when it did not.</summary>
    public string LoadError { get; private set; } = string.Empty;

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

    /// <summary>Swaps in a refreshed log in one step.</summary>
    public void ReplaceGraph(IReadOnlyList<GraphEntry> entries)
    {
        GraphReplacing?.Invoke();
        Graph.Replace(entries);
        GraphVersion++;
        Source.NotifyAppended();
        GraphReplaced?.Invoke();
        Changed?.Invoke();
    }

    public void Complete()
    {
        IsLoading = false;
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
        Diff?.Dispose();
        Cancellation.Cancel();
        Cancellation.Dispose();
    }
}
