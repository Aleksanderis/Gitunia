using Gitunia.App.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Workspaces.Repositories;

namespace Gitunia.App.Tabs;

/// <summary>
/// Checks, off the UI thread and in parallel, whether the directories of the restored tabs exist, and marks a tab missing
/// when its check says no or does not answer in time. A dead network drive or a stopped WSL can block a single
/// <c>Directory.Exists</c> for tens of seconds, so the startup restores every tab without asking the disk and this
/// runs after it; the blocked call itself cannot be interrupted, it just finishes unobserved.
/// </summary>
/// <param name="dispatcher">Where the missing marks go.</param>
/// <param name="files">The file system question.</param>
/// <param name="timeout">The longest one path may take before it counts as missing.</param>
public sealed class TabExistenceChecker(IDispatcher dispatcher, IDirectoryLister files, TimeSpan timeout)
{
    /// <summary>Starts one check per tab and returns a task that ends when all checks are done.</summary>
    /// <param name="tabs">The tabs to check.</param>
    /// <param name="cancellationToken">Ends the waiting when the app is closing.</param>
    public Task Start(IEnumerable<RepositoryState> tabs, CancellationToken cancellationToken) =>
        Task.WhenAll([.. tabs.Select(tab => CheckAsync(tab.Id, tab.CommandPath, cancellationToken))]);

    private async Task CheckAsync(TabId id, string path, CancellationToken cancellationToken)
    {
        var exists = false;
        try
        {
            exists = await Task.Run(() => files.DirectoryExists(path), CancellationToken.None)
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // An unanswered or failing check counts as missing.
        }

        if (!exists)
        {
            dispatcher.Post(new TabCommand(id, new SetMissingCommand(true)));
        }
    }
}
