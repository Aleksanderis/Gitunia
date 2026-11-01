using Gitunia.App.Tabs;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tests.Fakes;

/// <summary>A tab session without the terminal side: it keeps the streamed rows and the load state, nothing else.</summary>
public sealed class FakeGraphSession(TabId tabId) : IGraphSession, IDisposable
{
    public event Action? LoadFinished;

    public TabId TabId { get; } = tabId;

    public CommitGraph Graph { get; } = new();

    public int GraphVersion { get; private set; }

    public CancellationTokenSource Cancellation { get; } = new();

    public bool IsPending { get; private set; } = true;

    public bool IsLoading { get; private set; } = true;

    public bool IsTruncated => TruncatedTotal is not null;

    public int? TruncatedTotal { get; private set; }

    public string LoadError { get; private set; } = string.Empty;

    public void NotifyStatusRead()
    {
    }

    public void Append(IReadOnlyList<GraphEntry> batch) => Graph.AddRange(batch);

    public void Truncate(int total) => TruncatedTotal = total;

    public void Complete()
    {
        IsPending = false;
        IsLoading = false;
        LoadFinished?.Invoke();
    }

    public void Fail(string message)
    {
        IsLoading = false;
        LoadError = message;
        LoadFinished?.Invoke();
    }

    public void CompleteExpansion()
    {
        TruncatedTotal = null;
        Complete();
    }

    public void EndExpansion()
    {
        IsLoading = false;
        LoadFinished?.Invoke();
    }

    public void ReplaceGraph(IReadOnlyList<GraphEntry> entries, int? truncatedTotal = null)
    {
        TruncatedTotal = truncatedTotal;
        IsLoading = false;
        Graph.Replace(entries);
        GraphVersion++;
    }

    public void Dispose() => Cancellation.Dispose();
}
