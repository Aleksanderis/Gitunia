using Gitunia.Core.Workspaces;
using Gitunia.Core.IssueTracker;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App;

// The tab housekeeping of the runner: busy tabs, issue links, and keeping only the warm tabs loaded.
public sealed partial class EffectRunner<TSession>
{
    // A tab runs an operation when its state says so or a legacy gate does; such a tab is never evicted.
    private bool IsBusy(TabId tabId) => IsTabBusy(tabId) || _host.Workspace.FindTab(tabId)?.Running.IsBusy == true;

    private void ReloadIssueLinks(TabId tabId)
    {
        var tab = _host.Workspace.FindTab(tabId);
        if (tab is null || !_sessions.TryGetValue(tabId, out var session))
        {
            return;
        }

        var location = tab.Location;
        var token = session.Cancellation.Token;
        _ = Task.Run(() => QueueIssueLinksAsync(session, location, token), CancellationToken.None);
    }

    private async Task QueueIssueLinksAsync(TSession session, RepositoryLocation location, CancellationToken token)
    {
        try
        {
            await _background.RunAsync(() => LoadIssueLinksAsync(session, location, token), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The tab closed while the read waited for a background slot.
        }
    }

    // The tracker file and git config are read on load and on a manual refresh; a missing or bad one means no links.
    private async Task LoadIssueLinksAsync(TSession session, RepositoryLocation location, CancellationToken token)
    {
        try
        {
            var rules = await _services.IssueTracker.ReadAsync(location, token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var links = rules.Count > 0 ? new IssueTrackerLinks(rules) : IssueTrackerLinks.None;
            _postToUi(() => session.SetIssueLinks(links));
        }
        catch (Exception exception) when (exception is OperationCanceledException or GitException)
        {
            // Cancelled with the tab, or git is unavailable: the detail pane shows no links.
        }
    }

    private void OnHostChanged()
    {
        StartActiveTabLoad();
        var activeId = _host.Workspace.ActiveTabId;
        if (activeId is { } shownId)
        {
            _recent.Remove(shownId);
            _recent.Insert(0, shownId);
            EvictColdTabs();
        }

        foreach (var (tabId, refresher) in _refreshers)
        {
            refresher.SetActive(tabId == activeId);
        }
    }

    private void StartActiveTabLoad()
    {
        if (_pendingLoads.Count > 0 && _host.Workspace.ActiveTabId is { } active && _pendingLoads.Remove(active, out var start))
        {
            start();
        }
    }

    // A tab beyond the most recently used ones drops its rows, caches and watcher and goes back to the pending state:
    // its cursor, scroll and selected commit live in the workspace state, so activating it again reloads in the
    // background and puts them back. A tab still loading or running an operation stays.
    // The tab's search and diff positions are not kept.
    private void EvictColdTabs()
    {
        for (var index = _warmTabLimit; index < _recent.Count; index++)
        {
            var tabId = _recent[index];
            if (_sessions.TryGetValue(tabId, out var session) && !session.IsPending && !session.IsLoading
                && !IsBusy(tabId)
                && _host.Workspace.FindTab(tabId) is { } tab)
            {
                StartLoad(new LoadRepositoryEffect(tabId, tab.CommandPath));
            }
        }
    }
}
