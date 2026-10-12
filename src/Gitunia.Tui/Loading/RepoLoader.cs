using Gitunia.Core.Status;
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
    /// <param name="statuses">Told when a part of the load fails.</param>
    /// <param name="postToUi">Runs an action on the UI thread.</param>
    /// <param name="token">Cancelled when the tab closes; taken by the caller because the session disposes its source.</param>
    public static async Task RunAsync(GitRunner runner, RepositoryLocation location, TabSession session, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        var repo = new Repo(runner, location);
        var metadata = LoadMetadataAsync(repo, session.TabId, host, statuses, postToUi, token);
        var sidebarData = SidebarDataLoader.LoadAsync(repo, session.TabId, host, statuses, postToUi, token);
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
            postToUi(() =>
            {
                statuses.Error("Could not load the graph: " + exception.Message);
                session.Fail(exception.Message);
            });
        }

        await metadata.ConfigureAwait(false);
        await sidebarData.ConfigureAwait(false);
    }

    private static async Task LoadMetadataAsync(Repo repo, TabId tabId, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        try
        {
            var headTask = repo.ReadHeadAsync(token);
            var refsTask = RefReader.ReadRefsAsync(repo, token);
            var statusTask = StatusReader.ReadStatusAsync(repo, false, token);
            var head = await headTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new HeadLoadedCommand(head))));
            var refs = await refsTask.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new RefsLoadedCommand([.. refs.Items]))));
            var status = await statusTask.ConfigureAwait(false);
            var summary = new RepoStatusSummary(status.Entries.Count > 0, status.Branch.Ahead ?? 0, status.Branch.Behind ?? 0);
            postToUi(() => host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary))));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (GitException exception)
        {
            // The graph load reports its own failure; this part would otherwise leave the tab label and branch silently empty.
            postToUi(() => statuses.Error(Path.GetFileName(Path.TrimEndingDirectorySeparator(repo.Location.RootPath)) + ": could not read the repository status: " + exception.Message));
        }
    }
}
