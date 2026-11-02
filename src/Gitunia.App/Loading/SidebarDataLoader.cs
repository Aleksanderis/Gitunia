using Gitunia.Core.Text;
using Gitunia.App.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;

namespace Gitunia.App.Loading;

/// <summary>Loads what the sidebar lists besides branches and tags (worktrees and stashes) and hands each result to the UI thread.</summary>
public sealed class SidebarDataLoader
{
    private readonly IRefReader _refs;
    private readonly IStashReader _stashes;

    public SidebarDataLoader(IRefReader refs, IStashReader stashes)
    {
        _refs = refs;
        _stashes = stashes;
    }

    public async Task LoadAsync(RepositoryLocation location, TabId tabId, Dispatcher host, StatusReporter statuses, IUiInvoker ui, CancellationToken token)
    {
        var worktreesTask = _refs.ReadWorktreesAsync(location, token);
        var stashesTask = _stashes.ReadStashesAsync(location, token);
        await PostAsync(worktreesTask, worktrees => new WorktreesLoadedCommand([.. worktrees.Items]), "worktrees", location, tabId, host, statuses, ui).ConfigureAwait(false);
        await PostAsync(stashesTask, stashes => new StashesLoadedCommand([.. stashes.Items]), "stashes", location, tabId, host, statuses, ui).ConfigureAwait(false);
    }

    private static async Task PostAsync<TResult>(Task<TResult> task, Func<TResult, RepositoryCommand> toCommand, string what, RepositoryLocation location, TabId tabId, Dispatcher host, StatusReporter statuses, IUiInvoker ui)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            ui.Post(() => host.Dispatch(new TabCommand(tabId, toCommand(result))));
        }
        catch (OperationCanceledException)
        {
            // The tab was closed.
        }
        catch (GitException exception)
        {
            // One failed part does not hide the other.
            ui.Post(() => statuses.Error($"{Path.GetFileName(Path.TrimEndingDirectorySeparator(location.RootPath))}: could not read the {what}: {FailureText.Reason(exception)}"));
        }
    }
}
