using Gitunia.App.Background;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;

namespace Gitunia.App;

/// <summary>
/// Keeps the tab bar markers (dirty, ahead, behind) right for tabs that are not shown: runs <c>git status</c> only (no graph, no refs, no
/// watchers) for one background tab at a time, once at start and then on a slow timer. Nothing else is loaded for those tabs
/// (docs/internal/performance.md, "Many tabs"). The active tab is skipped; its own refresher keeps it current.
/// </summary>
public sealed class BackgroundStatusPoller : IDisposable
{
    private readonly IStatusReader _status;
    private readonly Dispatcher _host;
    private readonly IUiInvoker _ui;
    private readonly TimeSpan _interval;
    private readonly IBackgroundWork _background;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ShutdownTasks _tasks = new();
    private bool _started;

    public BackgroundStatusPoller(IStatusReader status, Dispatcher host, IUiInvoker ui, TimeSpan? interval = null, IBackgroundWork? background = null)
    {
        _background = background ?? UnboundedBackgroundWork.Instance;
        _status = status;
        _host = host;
        _ui = ui;
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Starts the loop: one pass now, then one per interval. Calling it twice has no effect.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        var token = _cancellation.Token;
        _tasks.Run(() => RunAsync(token));
    }

    /// <summary>
    /// Waits until the loop has ended after <see cref="Dispose"/> (its git process is gone, so the repository directory is free)
    /// or the timeout has passed; true when it has ended.
    /// </summary>
    public bool WaitForStop(TimeSpan timeout) => _tasks.Wait(timeout);

    /// <summary>Reads the status of every background tab once, sequentially, and updates the markers that changed.</summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var workspace = _host.Workspace;
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
                var status = await _status.ReadStatusAsync(tab.Location, false, UntrackedFilesMode.Normal, cancellationToken).ConfigureAwait(false);
                var summary = RepositoryStatusSummary.From(status);
                var tabId = tab.Id;
                _ui.Post(() => Apply(tabId, summary));
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
        _cancellation.Cancel();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(_interval);
        do
        {
            await RunPassAsync(token).ConfigureAwait(false);
        }
        while (await WaitAsync(timer, token).ConfigureAwait(false));
    }

    private async Task RunPassAsync(CancellationToken token)
    {
        try
        {
            await _background.RunAsync(() => PollOnceAsync(token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Disposed while waiting for a background slot.
        }
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
        if (_cancellation.IsCancellationRequested || _host.Workspace.ActiveTabId == tabId || _host.Workspace.FindTab(tabId) is not { } tab || tab.Status == summary)
        {
            return;
        }

        _host.Dispatch(new TabCommand(tabId, new StatusLoadedCommand(summary)));
    }
}
