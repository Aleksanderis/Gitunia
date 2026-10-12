using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;

namespace Gitunia.Tui.Loading;

/// <summary>Loads what the sidebar lists besides branches and tags (worktrees and stashes) and hands each result to the UI thread.</summary>
public static class SidebarDataLoader
{
    public static async Task LoadAsync(Repo repo, TabId tabId, WorkspaceHost host, Action<Action> postToUi, CancellationToken token)
    {
        var worktreesTask = RefReader.ReadWorktreesAsync(repo, token);
        var stashesTask = StashReader.ReadStashesAsync(repo, token);
        await PostAsync(worktreesTask, worktrees => new WorktreesLoadedCommand([.. worktrees.Items]), tabId, host, postToUi).ConfigureAwait(false);
        await PostAsync(stashesTask, stashes => new StashesLoadedCommand([.. stashes.Items]), tabId, host, postToUi).ConfigureAwait(false);
    }

    private static async Task PostAsync<TResult>(Task<TResult> task, Func<TResult, RepoCommand> toCommand, TabId tabId, WorkspaceHost host, Action<Action> postToUi)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            postToUi(() => host.Dispatch(new TabCommand(tabId, toCommand(result))));
        }
        catch (Exception exception) when (exception is OperationCanceledException or GitException)
        {
            // Cancelled with the tab, or git failed; one failed part does not hide the other.
        }
    }
}
