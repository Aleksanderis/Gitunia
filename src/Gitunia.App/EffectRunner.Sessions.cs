using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Stash;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Tabs;
using Gitunia.Core.IssueTracker;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.App;

// The per-tab sessions: loading a tab, the issue links, the graph expansion and the eviction of cold tabs.
public sealed partial class EffectRunner<TSession>
{
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

    private void StartLoad(LoadRepositoryEffect load)
    {
        var tab = _host.Workspace.FindTab(load.TabId);
        if (tab is null)
        {
            return;
        }

        _pendingLoads.Remove(load.TabId);
        DisposeRefresher(load.TabId);
        if (_sessions.Remove(load.TabId, out var previous))
        {
            previous.Dispose();
        }

        var stashView = new StashViewSession();
        var session = _sessionFactory.Create(
            new TabSessionParts(
                load.TabId,
                new CommitDetailsStore(
                    new StashAwareCommitDetailsReader(
                        _services.CommitDetails,
                        _services.StashDetails,
                        stashView.Registry),
                    tab.Location,
                    _postToUi,
                    _detailDebounce,
                    background: _background),
                new DiffStore(
                    new StashAwareDiffReader(_services.Diff, _services.StashDetails, stashView.Registry),
                    tab.Location,
                    _postToUi,
                    background: _background),
                new WorkingDiffStore(_services.Diff, tab.Location, _postToUi),
                new FileTreeSession(
                    new CommitTreeStore(_services.CommitTree, tab.Location, _postToUi, _detailDebounce)),
                new FileContentStore(_services.Blob, tab.Location, _postToUi),
                stashView));
        _sessions[load.TabId] = session;
        var token = session.Cancellation.Token;
        var location = tab.Location;
        session.ExpandRequested += () => ExpandGraph(session, location, token);

        // Only the active tab loads now; background tabs keep an empty session until first activated
        // (docs/internal/performance.md, "Many tabs").
        if (_host.Workspace.ActiveTabId == load.TabId)
        {
            StartSession(load.TabId, session, location, token);
        }
        else
        {
            _pendingLoads[load.TabId] = () => StartSession(load.TabId, session, location, token);
        }
    }

    private void StartSession(TabId tabId, TSession session, RepositoryLocation location, CancellationToken token)
    {
        session.MarkStarted();
        var refresher = new TabRefresher(
            _services.Refresher,
            _services.Stashes,
            _services.IncrementalGraphLoader,
            _host,
            session,
            location,
            _statuses,
            _postToUi,
            _watchBudget,
            _watcherOptions,
            operations: _services.OperationInfo,
            config: _services.Config,
            directoryLister: OperatingSystem.IsLinux() ? _services.WatchedDirectories : null,
            poll: _pollOptions,
            isBusy: () => IsBusy(tabId));
        _refreshers[tabId] = refresher;
        refresher.Start();
        refresher.SetActive(_host.Workspace.ActiveTabId == tabId);
        _ = Task.Run(() => QueueIssueLinksAsync(session, location, token), CancellationToken.None);
        _ = Task.Run(
            () => _services.RepositoryLoader.RunAsync(location, session, _host, _statuses, _postToUi, token),
            CancellationToken.None);
    }

    private async Task QueueIssueLinksAsync(TSession session, RepositoryLocation location, CancellationToken token)
    {
        try
        {
            await _background
                .RunAsync(() => LoadIssueLinksAsync(session, location, token), token)
                .ConfigureAwait(false);
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

    // Runs on the UI thread (the session asked for it), so the graph can be read here.
    private void ExpandGraph(TSession session, RepositoryLocation location, CancellationToken token)
    {
        var shownCount = session.Graph.Count;
        var lastShown = session.Graph[shownCount - 1].Id;
        var version = session.GraphVersion;
        _ = Task.Run(
            () => _services.RepositoryLoader.ExpandAsync(
                location,
                session,
                _statuses,
                _postToUi,
                shownCount,
                lastShown,
                version,
                token),
            CancellationToken.None);
    }

    private void DisposeRefresher(TabId tabId)
    {
        if (_refreshers.Remove(tabId, out var refresher))
        {
            refresher.Dispose();
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
        if (_pendingLoads.Count > 0
            && _host.Workspace.ActiveTabId is { } active
            && _pendingLoads.Remove(active, out var start))
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
