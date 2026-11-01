using Gitunia.App.Background;
using System.Collections.Immutable;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status;
using Gitunia.Git.Status.Models;
using Gitunia.Git.Worktrees;

namespace Gitunia.App.Worktrees;

/// <summary>
/// Keeps the sidebar's worktree overview right: reads <c>git status</c> for every worktree of the active tab except
/// the one the tab shows (its own status is kept current by the tab's refresher), one at a time. A pass runs when the
/// active tab or its worktree list changes and then on a slow timer, because changes in the other worktrees raise no
/// event for this tab.
/// Entries that cannot be read keep their last value; worktrees that are gone from the list drop out.
/// </summary>
public sealed class WorktreeOverviewPoller : IDisposable
{
    private readonly IStatusReader _status;
    private readonly Dispatcher _host;
    private readonly Action<Action> _postToUi;
    private readonly TimeSpan _interval;
    private readonly IBackgroundWork _background;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private TabId? _watchedTabId;
    private ImmutableArray<WorktreeInfo> _watchedWorktrees;
    private string? _watchedPath;
    private Task? _loop;

    public WorktreeOverviewPoller(
        IStatusReader status,
        Dispatcher host,
        Action<Action> postToUi,
        TimeSpan? interval = null,
        IBackgroundWork? background = null)
    {
        _background = background ?? UnboundedBackgroundWork.Instance;
        _status = status;
        _host = host;
        _postToUi = postToUi;
        _interval = interval ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>Starts the loop and the watch for changes of the active tab. Calling it twice has no effect.</summary>
    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _host.Changed += OnHostChanged;
        _loop = Task.Run(() => RunAsync(_cancellation.Token), CancellationToken.None);
    }

    /// <summary>Reads the status of the active tab's other worktrees once and updates the overview.</summary>
    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        if (_host.Workspace.ActiveTab is not { } tab || tab.Worktrees.Length < 2)
        {
            return;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, WorktreeStatus>(RepositoryPathComparer.Instance);
        if (tab.WorktreeStatuses is { } previous)
        {
            builder.AddRange(previous);
        }

        var listed = new HashSet<string>(RepositoryPathComparer.Instance);
        foreach (var worktree in tab.Worktrees)
        {
            listed.Add(worktree.Path);
            var isOwn = RepositoryPathComparer.Instance.Equals(worktree.Path, tab.CommandPath);
            if (worktree.IsBare || worktree.IsPrunable || isOwn)
            {
                builder.Remove(worktree.Path);
                continue;
            }

            if (await ReadAsync(tab.Location, worktree.Path, cancellationToken).ConfigureAwait(false) is { } status)
            {
                builder[worktree.Path] = status;
            }
        }

        foreach (var path in builder.Keys.Where(path => !listed.Contains(path)).ToList())
        {
            builder.Remove(path);
        }

        var statuses = builder.ToImmutable();
        var tabId = tab.Id;
        _postToUi(() => Apply(tabId, tab.Location, statuses));
    }

    public void Dispose()
    {
        _host.Changed -= OnHostChanged;
        _cancellation.Cancel();
    }

    private async Task<WorktreeStatus?> ReadAsync(RepositoryLocation origin, string path, CancellationToken token)
    {
        // Only the working tree path matters to the status read, so the shared directories of the tab are reused.
        var location = new RepositoryLocation(path, origin.CommonDirectory, origin.CommonDirectory, false, true);
        try
        {
            var snapshot = await _status
                .ReadStatusAsync(location, false, UntrackedFilesMode.All, token)
                .ConfigureAwait(false);
            return WorktreeStatus.From(snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A worktree that cannot be read keeps its last value and never stops the others or the loop.
            return null;
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await PollGuardedAsync(token).ConfigureAwait(false);
                await _wake.WaitAsync(_interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed.
        }
    }

    // A failing pass (for example a posted action that throws) must not end the loop and freeze the overview.
    private async Task PollGuardedAsync(CancellationToken token)
    {
        try
        {
            await _background.RunAsync(() => PollOnceAsync(token), token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The next tick or change tries again.
        }
    }

    private void OnHostChanged()
    {
        var tab = _host.Workspace.ActiveTab;
        var tabId = tab?.Id;
        var worktrees = tab?.Worktrees ?? default;
        var path = tab?.CommandPath;
        if (tabId == _watchedTabId && worktrees == _watchedWorktrees && path == _watchedPath)
        {
            return;
        }

        _watchedTabId = tabId;
        _watchedWorktrees = worktrees;
        _watchedPath = path;
        WakeLoop();
    }

    // Changed is raised on the UI thread only. The semaphore holds at most one wake, so Release reporting that one is
    // already pending is not an error: it is a cheap guard instead of a check of CurrentCount before the Release.
    private void WakeLoop()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake is already pending; the loop will see the latest state when it runs.
        }
    }

    // The tab may have been closed, moved to another worktree or have its list changed while the reads ran.
    private void Apply(TabId tabId, RepositoryLocation location, ImmutableDictionary<string, WorktreeStatus> statuses)
    {
        if (_cancellation.IsCancellationRequested
            || _host.Workspace.FindTab(tabId) is not { } tab
            || tab.Location != location
            || SameStatuses(tab.WorktreeStatuses, statuses))
        {
            return;
        }

        _host.Dispatch(new TabCommand(tabId, new WorktreeStatusesLoadedCommand(statuses)));
    }

    private static bool SameStatuses(
        ImmutableDictionary<string, WorktreeStatus>? current,
        ImmutableDictionary<string, WorktreeStatus> next) =>
        current is not null
        && current.Count == next.Count
        && next.All(pair => current.TryGetValue(pair.Key, out var existing) && existing == pair.Value);
}
