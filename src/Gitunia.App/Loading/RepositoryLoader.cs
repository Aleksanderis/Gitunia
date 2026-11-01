using Gitunia.App.Loading.Graph;
using Gitunia.App.Tabs;
using Gitunia.Core.Graph;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;

namespace Gitunia.App.Loading;

/// <summary>Loads everything a freshly opened tab shows (HEAD, refs, the streamed graph) and hands each result to the UI thread.</summary>
public sealed class RepositoryLoader
{
    private readonly IRefReader _refs;
    private readonly IStatusReader _status;
    private readonly GraphLoader _graphLoader;
    private readonly SidebarDataLoader _sidebarLoader;

    public RepositoryLoader(IRefReader refs, IStatusReader status, GraphLoader graphLoader, SidebarDataLoader sidebarLoader)
    {
        _refs = refs;
        _status = status;
        _graphLoader = graphLoader;
        _sidebarLoader = sidebarLoader;
    }

    /// <param name="location">The repository of the tab.</param>
    /// <param name="session">Receives the graph rows.</param>
    /// <param name="host">Receives HEAD and refs as tab commands.</param>
    /// <param name="statuses">Told when a part of the load fails.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="token">Cancelled when the tab closes; taken by the caller because the session disposes its source.</param>
    public async Task RunAsync(RepositoryLocation location, IGraphSession session, Dispatcher host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        var metadata = LoadMetadataAsync(location, session.TabId, host, statuses, postToUi, token);
        var sidebarData = _sidebarLoader.LoadAsync(location, session.TabId, host, statuses, postToUi, token);
        try
        {
            var cut = new GraphCut();
            await foreach (var batch in _graphLoader.StreamCappedAsync(location, cut, token).ConfigureAwait(false))
            {
                postToUi(() => session.Append(batch));
            }

            if (cut.TruncatedTotal is { } total)
            {
                postToUi(() => session.Truncate(total));
            }

            postToUi(session.Complete);
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            // Not only git failures: a broken pipe or a bug must end the loading state with a message, never hang it.
            postToUi(() =>
            {
                statuses.Error($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath))}: could not load the graph: {exception.Message}");
                session.Fail(exception.Message);
            });
        }

        await metadata.ConfigureAwait(false);
        await sidebarData.ConfigureAwait(false);
    }

    /// <summary>
    /// Streams the rest of a cut log and appends it behind the rows the session already shows. The log is read again
    /// from the newest commit (the lane layout needs every row before), and the rows already shown are skipped. A
    /// refresh that replaces the graph meanwhile ends this quietly; a log that no longer starts with the shown rows
    /// ends it with a message.
    /// </summary>
    /// <param name="location">The repository of the tab.</param>
    /// <param name="session">Receives the rows.</param>
    /// <param name="statuses">Told when the log changed under the read or the read failed.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="shownCount">Rows the session shows now.</param>
    /// <param name="lastShown">The last of them, which the stream must reach at the same place.</param>
    /// <param name="graphVersion">The session's graph version now; a replacement makes this read stale.</param>
    /// <param name="token">Cancelled when the tab closes.</param>
    public async Task ExpandAsync(
        RepositoryLocation location,
        IGraphSession session,
        StatusChannel statuses,
        Action<Action> postToUi,
        int shownCount,
        ObjectId lastShown,
        int graphVersion,
        CancellationToken token)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath));
        var seen = 0;
        try
        {
            var stream = _graphLoader.StreamAsync(location, CommitOrder.ByDate, null, token);
            await foreach (var batch in stream.ConfigureAwait(false))
            {
                var skip = Math.Clamp(shownCount - seen, 0, batch.Count);
                if (skip > 0 && seen + skip == shownCount && batch[skip - 1].Id != lastShown)
                {
                    postToUi(() => ExpansionFailed(session, statuses, graphVersion, ChangedMessage(name)));

                    return;
                }

                seen += batch.Count;
                if (skip < batch.Count)
                {
                    var rest = skip == 0 ? batch : batch.Skip(skip).ToList();
                    postToUi(() => AppendExpansion(session, graphVersion, rest));
                }
            }

            if (seen < shownCount)
            {
                postToUi(() => ExpansionFailed(session, statuses, graphVersion, ChangedMessage(name)));

                return;
            }

            postToUi(() => CompleteExpansion(session, graphVersion));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (Exception exception)
        {
            var message = $"{name}: could not load the full history: {exception.Message}";
            postToUi(() => ExpansionFailed(session, statuses, graphVersion, message));
        }
    }

    private static string ChangedMessage(string name) => $"{name}: history changed while loading, press r to refresh";

    private static void AppendExpansion(IGraphSession session, int graphVersion, IReadOnlyList<GraphEntry> rows)
    {
        if (session.GraphVersion == graphVersion)
        {
            session.Append(rows);
        }
    }

    private static void CompleteExpansion(IGraphSession session, int graphVersion)
    {
        if (session.GraphVersion == graphVersion)
        {
            session.CompleteExpansion();
        }
    }

    private static void ExpansionFailed(IGraphSession session, StatusChannel statuses, int graphVersion, string message)
    {
        if (session.GraphVersion != graphVersion)
        {
            return;
        }

        statuses.Error(message);
        session.EndExpansion();
    }

    private async Task LoadMetadataAsync(RepositoryLocation location, TabId tabId, Dispatcher host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        try
        {
            var headTask = _refs.ReadHeadAsync(location, token);
            var refsTask = _refs.ReadRefsAsync(location, token);
            var statusTask = _status.ReadStatusAsync(location, false, UntrackedFilesMode.All, token);
            var head = await headTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(head))));
            var refs = await refsTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. refs.Items]))));
            var status = await statusTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new WorkingTreeLoadedCommand(status))));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (GitException exception)
        {
            // The graph load reports its own failure; this part would otherwise leave the tab label and branch silently empty.
            postToUi(() => statuses.Error($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath))}: could not read the repository status: {exception.Message}"));
        }
    }
}
