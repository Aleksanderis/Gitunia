using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>
/// What the loaders and the refresher need of one tab's runtime session: the streamed graph rows and the state of the
/// load. The terminal side of the session (list sources, glyphs) stays out of this interface, so the runtime does not
/// depend on it. UI thread only.
/// </summary>
public interface IGraphSession
{
    /// <summary>Raised when the initial load ended, successfully or not.</summary>
    event Action? LoadFinished;

    TabId TabId { get; }

    CommitGraph Graph { get; }

    /// <summary>Counts graph replacements; lets a loader notice that rows it read are stale.</summary>
    int GraphVersion { get; }

    CancellationTokenSource Cancellation { get; }

    /// <summary>The tab has not been activated since the session was restored, so its load has not started.</summary>
    bool IsPending { get; }

    /// <summary>The log is still streaming in.</summary>
    bool IsLoading { get; }

    /// <summary>The loaded rows are the newest part of a longer log.</summary>
    bool IsTruncated { get; }

    /// <summary>A refresh read the status and it did not change: the Changes view diff of the shown file is read again.</summary>
    void NotifyStatusRead();

    void Append(IReadOnlyList<GraphEntry> batch);

    /// <summary>Marks the loaded rows as the newest part of a log with <paramref name="total"/> commits.</summary>
    void Truncate(int total);

    void Complete();

    void Fail(string message);

    /// <summary>The rest of a cut log has been appended: the graph is whole now.</summary>
    void CompleteExpansion();

    /// <summary>Loading the rest of a cut log stopped early; the log stays cut and can be requested again.</summary>
    void EndExpansion();

    /// <summary>Swaps in a refreshed log in one step; <paramref name="truncatedTotal"/> is set if it was cut.</summary>
    void ReplaceGraph(IReadOnlyList<GraphEntry> entries, int? truncatedTotal = null);
}
