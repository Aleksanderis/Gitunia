using Gitunia.Core.IssueTracker;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.Tabs.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Loading.Trees;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.Loading.Diff;
using Gitunia.Tui.Loading.Stash;
using Gitunia.Tui.Rendering.Graph;

namespace Gitunia.Tui.App;

/// <summary>
/// Executes the effects the reducer returns and owns the per-tab sessions they create. New effects (clipboard,
/// file watching, ...) get a case in <see cref="Run"/>; results come back as commands through the host.
/// </summary>
public sealed class EffectRunner : IDisposable
{
    /// <summary>
    /// How many tabs, the active one included, keep their loaded graph; older tabs are evicted
    /// (docs/internal/performance.md, "Bounded memory").
    /// </summary>
    public const int DefaultWarmTabLimit = 5;

    private readonly GitServices _services;
    private readonly WorkspaceHost _host;
    private readonly Action<Action> _postToUi;
    private readonly StatusChannel _statuses;
    private GraphGlyphSet _glyphs;
    private readonly TimeSpan _detailDebounce;
    private readonly Dictionary<TabId, TabSession> _sessions = [];
    private readonly Dictionary<TabId, Action> _pendingLoads = [];
    private readonly Dictionary<TabId, TabRefresher> _refreshers = [];
    private readonly List<TabId> _recent = [];
    private readonly int _warmTabLimit;
    private readonly WatchBudget _watchBudget;
    private readonly RepositoryWatcherOptions _watcherOptions;
    private readonly PollOptions _pollOptions;

    public EffectRunner(
        GitServices services,
        WorkspaceHost host,
        Action<Action> postToUi,
        GraphGlyphSet glyphs,
        StatusChannel statuses,
        TimeSpan? detailDebounce = null,
        RepositoryWatcherOptions? watcherOptions = null,
        WatchBudget? watchBudget = null,
        int warmTabLimit = DefaultWarmTabLimit,
        PollOptions? pollOptions = null)
    {
        _pollOptions = pollOptions ?? PollOptions.Default;
        _warmTabLimit = Math.Max(1, warmTabLimit);
        _watcherOptions = watcherOptions ?? RepositoryWatcherOptions.Default;
        _watchBudget = watchBudget ?? new WatchBudget();
        _detailDebounce = detailDebounce ?? TimeSpan.FromMilliseconds(80);
        _services = services;
        _host = host;
        _postToUi = postToUi;
        _glyphs = glyphs;
        _statuses = statuses;
        host.EffectRaised += Run;
        host.Changed += OnHostChanged;
    }

    /// <summary>
    /// Says whether a tab runs a write or remote operation; such a tab is never evicted
    /// (closing its session would cancel the work).
    /// </summary>
    public Func<TabId, bool> IsTabBusy { get; set; } = static _ => false;

    /// <summary>Draws every tab's graph with a new glyph set (the label caps setting changed); later tabs take it too.</summary>
    /// <param name="glyphs">The new set.</param>
    public void ReplaceGlyphs(GraphGlyphSet glyphs)
    {
        _glyphs = glyphs;
        foreach (var session in _sessions.Values)
        {
            session.Source.Glyphs = glyphs;
        }
    }

    /// <summary>Cancels the running git work of every tab (a forced quit, #856).</summary>
    public void CancelAll()
    {
        foreach (var session in _sessions.Values)
        {
            session.Cancellation.Cancel(throwOnFirstException: false);
        }
    }

    /// <summary>The runtime session of an open tab, or null.</summary>
    public TabSession? SessionOf(TabId tabId) => _sessions.GetValueOrDefault(tabId);

    /// <summary>Manual refresh of a tab: reads everything again and reloads its graph. False when the tab has no watcher yet (not loaded).</summary>
    public bool Refresh(TabId tabId, bool announce = true)
    {
        if (!_refreshers.TryGetValue(tabId, out var refresher))
        {
            return false;
        }

        refresher.RefreshNow(announce);
        if (announce)
        {
            ReloadIssueLinks(tabId);
        }

        return true;
    }

    /// <summary>The terminal gained focus: the active tab checks for outside changes at once.</summary>
    public void CheckActiveTabNow()
    {
        if (_host.Workspace.ActiveTabId is { } active && _refreshers.TryGetValue(active, out var refresher))
        {
            refresher.CheckNow();
        }
    }

    /// <summary>Re-reads the status of a tab after the index changed and runs <paramref name="applied"/> once the new state is in. False when the tab has no watcher yet.</summary>
    public bool RefreshStatus(TabId tabId, Action applied)
    {
        if (!_refreshers.TryGetValue(tabId, out var refresher))
        {
            return false;
        }

        refresher.RefreshStatus(applied);
        return true;
    }

    public void Dispose()
    {
        foreach (var refresher in _refreshers.Values)
        {
            refresher.Dispose();
        }

        _refreshers.Clear();
    }

    private void Run(WorkspaceEffect effect)
    {
        switch (effect)
        {
            case LoadRepositoryEffect load:
                StartLoad(load);
                break;
            case CancelLoadEffect cancel:
                _pendingLoads.Remove(cancel.TabId);
                _recent.Remove(cancel.TabId);
                DisposeRefresher(cancel.TabId);
                if (_sessions.Remove(cancel.TabId, out var closed))
                {
                    closed.Dispose();
                }

                break;
        }
    }

    private void ReloadIssueLinks(TabId tabId)
    {
        var tab = _host.Workspace.FindTab(tabId);
        if (tab is null || !_sessions.TryGetValue(tabId, out var session))
        {
            return;
        }

        var location = tab.Location;
        var token = session.Cancellation.Token;
        _ = Task.Run(() => LoadIssueLinksAsync(session, location, token), CancellationToken.None);
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
        var session = new TabSession(
            load.TabId,
            _glyphs,
            new CommitDetailsStore(
                new StashAwareCommitDetailsReader(_services.CommitDetails, _services.StashDetails, stashView.Registry),
                tab.Location,
                _postToUi,
                _detailDebounce),
            new DiffStore(
                new StashAwareDiffReader(_services.Diff, _services.StashDetails, stashView.Registry),
                tab.Location,
                _postToUi),
            new WorkingDiffStore(_services.Diff, tab.Location, _postToUi),
            new FileTreeSession(new CommitTreeStore(_services.CommitTree, tab.Location, _postToUi, _detailDebounce)),
            new FileContentStore(_services.Blob, tab.Location, _postToUi),
            stashView);
        _sessions[load.TabId] = session;
        var token = session.Cancellation.Token;
        var location = tab.Location;
        session.ExpandRequested += () => ExpandGraph(session, location, token);
        void Start()
        {
            session.MarkStarted();
            var refresher = new TabRefresher(
                _services.Refresher,
                _services.Stashes,
                _services.IncrementalGraphLoader,
                _host,
                session,
                tab.Location,
                _statuses,
                _postToUi,
                _watchBudget,
                _watcherOptions,
                operations: _services.OperationInfo,
                config: _services.Config,
                directoryLister: OperatingSystem.IsLinux() ? _services.WatchedDirectories : null,
                poll: _pollOptions,
                isBusy: () => IsTabBusy(load.TabId));
            _refreshers[load.TabId] = refresher;
            refresher.Start();
            refresher.SetActive(_host.Workspace.ActiveTabId == load.TabId);
            _ = Task.Run(() => LoadIssueLinksAsync(session, location, token), CancellationToken.None);
            _ = Task.Run(() => _services.RepositoryLoader.RunAsync(location, session, _host, _statuses, _postToUi, token), CancellationToken.None);
        }

        // Only the active tab loads now; background tabs keep an empty session until first activated (docs/internal/performance.md, "Many tabs").
        if (_host.Workspace.ActiveTabId == load.TabId)
        {
            Start();
        }
        else
        {
            _pendingLoads[load.TabId] = Start;
        }
    }

    // The tracker file and git config are read on load and on a manual refresh; a missing or bad one means no links.
    private async Task LoadIssueLinksAsync(TabSession session, RepositoryLocation location, CancellationToken token)
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
    private void ExpandGraph(TabSession session, RepositoryLocation location, CancellationToken token)
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
                && !IsTabBusy(tabId)
                && _host.Workspace.FindTab(tabId) is { } tab)
            {
                StartLoad(new LoadRepositoryEffect(tabId, tab.CommandPath));
            }
        }
    }
}
