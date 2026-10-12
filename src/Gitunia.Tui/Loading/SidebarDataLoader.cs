using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Loading;

/// <summary>Loads what the sidebar lists besides branches and tags (worktrees and stashes) and hands each result to the UI thread.</summary>
public static class SidebarDataLoader
{
    public static async Task LoadAsync(Repo repo, TabId tabId, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi, CancellationToken token)
    {
        var worktreesTask = RefReader.ReadWorktreesAsync(repo, token);
        var stashesTask = StashReader.ReadStashesAsync(repo, token);
        await PostAsync(worktreesTask, worktrees => new WorktreesLoadedCommand([.. worktrees.Items]), "worktrees", tabId, host, statuses, postToUi).ConfigureAwait(false);
        await PostAsync(stashesTask, stashes => new StashesLoadedCommand([.. stashes.Items]), "stashes", tabId, host, statuses, postToUi).ConfigureAwait(false);
    }

    private static async Task PostAsync<TResult>(Task<TResult> task, Func<TResult, RepoCommand> toCommand, string what, TabId tabId, WorkspaceHost host, StatusChannel statuses, Action<Action> postToUi)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, toCommand(result))));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (GitException exception)
        {
            // One failed part does not hide the other.
            postToUi(() => statuses.Error($"Could not read the {what}: {exception.Message}"));
        }
    }
}
