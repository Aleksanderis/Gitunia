using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Loading;

/// <summary>Loads everything a freshly opened tab shows (HEAD, refs, the streamed graph) and hands each result to the UI thread.</summary>
public static class RepoLoader
{
    /// <param name="runner">The git runner.</param>
    /// <param name="location">The repository of the tab.</param>
    /// <param name="session">Receives the graph rows.</param>
    /// <param name="host">Receives HEAD and refs as tab commands.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="token">Cancelled when the tab closes; taken by the caller because the session disposes its source.</param>
    public static async Task RunAsync(GitRunner runner, RepositoryLocation location, TabSession session, WorkspaceHost host, Action<Action> postToUi, CancellationToken token)
    {
        var repo = new Repo(runner, location);
        var metadata = LoadMetadataAsync(repo, session.TabId, host, postToUi, token);
        var sidebarData = SidebarDataLoader.LoadAsync(repo, session.TabId, host, postToUi, token);
        try
        {
            await foreach (var batch in GraphLoader.StreamAsync(repo, CommitOrder.Topological, null, token).ConfigureAwait(false))
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
            postToUi(() => session.Fail(exception.Message));
        }

        await metadata.ConfigureAwait(false);
        await sidebarData.ConfigureAwait(false);
    }

    private static async Task LoadMetadataAsync(Repo repo, TabId tabId, WorkspaceHost host, Action<Action> postToUi, CancellationToken token)
    {
        try
        {
            var headTask = repo.ReadHeadAsync(token);
            var refsTask = RefReader.ReadRefsAsync(repo, token);
            var head = await headTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(head))));
            var refs = await refsTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. refs.Items]))));
        }
        catch (Exception exception) when (exception is OperationCanceledException or GitException)
        {
            // Cancelled with the tab, or git failed; the graph load reports its own failure.
        }
    }
}
