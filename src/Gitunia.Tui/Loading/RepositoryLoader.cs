using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Loading.Graph;

namespace Gitunia.Tui.Loading;

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
    public async Task RunAsync(RepositoryLocation location, TabSession session, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        var metadata = LoadMetadataAsync(location, session.TabId, host, statuses, postToUi, token);
        var sidebarData = _sidebarLoader.LoadAsync(location, session.TabId, host, statuses, postToUi, token);
        try
        {
            await foreach (var batch in _graphLoader.StreamAsync(location, CommitOrder.Topological, null, token).ConfigureAwait(false))
            {
                postToUi(() => session.Append(batch));
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

    private async Task LoadMetadataAsync(RepositoryLocation location, TabId tabId, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
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
            var summary = RepositoryStatusSummary.From(status);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary))));
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
