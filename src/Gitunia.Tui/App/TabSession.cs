using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App;

/// <summary>
/// The runtime side of one tab that the immutable <see cref="RepoState"/> does not hold: the streamed graph rows,
/// the list source drawing them and the load in flight. Touched from the UI thread only.
/// </summary>
public sealed class TabSession : IDisposable
{
    public TabSession(TabId tabId, GraphGlyphSet glyphs)
    {
        TabId = tabId;
        Graph = new CommitGraph();
        Source = new GraphListSource(Graph, glyphs);
    }

    /// <summary>Raised after rows were appended or the load state changed.</summary>
    public event Action? Changed;

    public TabId TabId { get; }

    public CommitGraph Graph { get; }

    public GraphListSource Source { get; }

    public CancellationTokenSource Cancellation { get; } = new();

    /// <summary>The log is still streaming in.</summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>Why loading stopped early, empty when it did not.</summary>
    public string LoadError { get; private set; } = string.Empty;

    public void Append(IReadOnlyList<GraphEntry> batch)
    {
        Graph.AddRange(batch);
        Source.NotifyAppended();
        Changed?.Invoke();
    }

    public void Complete()
    {
        IsLoading = false;
        Changed?.Invoke();
    }

    public void Fail(string message)
    {
        IsLoading = false;
        LoadError = message;
        Changed?.Invoke();
    }

    public void Dispose()
    {
        Cancellation.Cancel();
        Cancellation.Dispose();
    }
}
