using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Repositories;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;

namespace Gitunia.Tui.App;

/// <summary>
/// Keeps the tab bar markers (dirty, ahead, behind) right for tabs that are not shown: runs <c>git status</c> only (no graph, no refs, no
/// watchers) for one background tab at a time, once at start and then on a slow timer. Nothing else is loaded for those tabs
/// (docs/architecture.md, "Many tabs"). The active tab is skipped; its own refresher keeps it current.
/// </summary>
public sealed class BackgroundStatusPoller : IDisposable
{
    private readonly GitRunner runner;
    private readonly WorkspaceHost host;
    private readonly Action<Action> postToUi;
    private readonly TimeSpan interval;
    private readonly CancellationTokenSource cancellation = new();
    private Task? loop;

    public BackgroundStatusPoller(GitRunner runner, WorkspaceHost host, Action<Action> postToUi, TimeSpan? interval = null)
    {
        this.runner = runner;
        this.host = host;
        this.postToUi = postToUi;
        this.interval = interval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Starts the loop: one pass now, then one per interval. Calling it twice has no effect.</summary>
    public void Start() => loop ??= Task.Run(() => RunAsync(cancellation.Token), CancellationToken.None);

    /// <summary>Reads the status of every background tab once, sequentially, and updates the markers that changed.</summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var workspace = host.Workspace;
        foreach (var tab in workspace.Tabs)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (tab.Id == workspace.ActiveTabId || tab.IsMissing)
            {
                continue;
            }

            try
            {
                var status = await StatusReader.ReadStatusAsync(new Repository(runner, tab.Location), false, UntrackedFilesMode.Normal, cancellationToken).ConfigureAwait(false);
                var summary = RepositoryStatusSummary.From(status);
                var tabId = tab.Id;
                postToUi(() => Apply(tabId, summary));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // A background tab that cannot be read keeps its last markers (the error shows when the tab is opened); it must never stop the other tabs or the loop.
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(interval);
        do
        {
            await PollOnceAsync(token).ConfigureAwait(false);
        }
        while (await WaitAsync(timer, token).ConfigureAwait(false));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void Apply(TabId tabId, RepositoryStatusSummary summary)
    {
        // The tab may have been activated while the read ran; its own loader and refresher then own the status.
        if (cancellation.IsCancellationRequested || host.Workspace.ActiveTabId == tabId || host.Workspace.FindTab(tabId) is not { } tab || tab.Status == summary)
        {
            return;
        }

        host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary)));
    }
}
