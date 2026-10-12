using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui.App;

/// <summary>
/// Executes the effects the reducer returns and owns the per-tab sessions they create. New effects (clipboard,
/// file watching, ...) get a case in <see cref="Run"/>; results come back as commands through the host.
/// </summary>
public sealed class EffectRunner : IDisposable
{
    private readonly GitRunner runner;
    private readonly WorkspaceHost host;
    private readonly Action<Action> postToUi;
    private readonly StatusChannel statuses;
    private readonly GraphGlyphSet glyphs;
    private readonly TimeSpan detailDebounce;
    private readonly Dictionary<TabId, TabSession> sessions = [];
    private readonly Dictionary<TabId, Action> pendingLoads = [];
    private readonly Dictionary<TabId, TabRefresher> refreshers = [];
    private readonly WatchBudget watchBudget;
    private readonly RepositoryWatcherOptions watcherOptions;

    public EffectRunner(GitRunner runner, WorkspaceHost host, Action<Action> postToUi, GraphGlyphSet glyphs, StatusChannel statuses, TimeSpan? detailDebounce = null, RepositoryWatcherOptions? watcherOptions = null, WatchBudget? watchBudget = null)
    {
        this.watcherOptions = watcherOptions ?? RepositoryWatcherOptions.Default;
        this.watchBudget = watchBudget ?? new WatchBudget();
        this.detailDebounce = detailDebounce ?? TimeSpan.FromMilliseconds(80);
        this.runner = runner;
        this.host = host;
        this.postToUi = postToUi;
        this.glyphs = glyphs;
        this.statuses = statuses;
        host.EffectRaised += Run;
        host.Changed += OnHostChanged;
    }

    /// <summary>The runtime session of an open tab, or null.</summary>
    public TabSession? SessionOf(TabId tabId) => sessions.GetValueOrDefault(tabId);

    /// <summary>Manual refresh of a tab: reads everything again and reloads its graph. False when the tab has no watcher yet (not loaded).</summary>
    public bool Refresh(TabId tabId)
    {
        if (!refreshers.TryGetValue(tabId, out var refresher))
        {
            return false;
        }

        refresher.RefreshNow();
        return true;
    }

    public void Dispose()
    {
        foreach (var refresher in refreshers.Values)
        {
            refresher.Dispose();
        }

        refreshers.Clear();
    }

    private void Run(WorkspaceEffect effect)
    {
        switch (effect)
        {
            case LoadRepositoryEffect load:
                StartLoad(load);
                break;
            case CancelLoadEffect cancel:
                pendingLoads.Remove(cancel.TabId);
                DisposeRefresher(cancel.TabId);
                if (sessions.Remove(cancel.TabId, out var closed))
                {
                    closed.Dispose();
                }

                break;
        }
    }

    private void StartLoad(LoadRepositoryEffect load)
    {
        var tab = host.Workspace.FindTab(load.TabId);
        if (tab is null)
        {
            return;
        }

        pendingLoads.Remove(load.TabId);
        DisposeRefresher(load.TabId);
        if (sessions.Remove(load.TabId, out var previous))
        {
            previous.Dispose();
        }

        var repo = new Repo(runner, tab.Location);
        var session = new TabSession(load.TabId, glyphs, new CommitDetailsStore(repo, postToUi, detailDebounce), new DiffStore(repo, postToUi));
        sessions[load.TabId] = session;
        var token = session.Cancellation.Token;
        var location = tab.Location;
        void Start()
        {
            session.MarkStarted();
            var refresher = new TabRefresher(runner, host, session, tab.Location, statuses, postToUi, watchBudget, watcherOptions);
            refreshers[load.TabId] = refresher;
            refresher.Start();
            refresher.SetActive(host.Workspace.ActiveTabId == load.TabId);
            _ = Task.Run(() => RepoLoader.RunAsync(runner, location, session, host, statuses, postToUi, token), CancellationToken.None);
        }

        // Only the active tab loads now; background tabs keep an empty session until first activated (docs/architecture.md, "Many tabs").
        if (host.Workspace.ActiveTabId == load.TabId)
        {
            Start();
        }
        else
        {
            pendingLoads[load.TabId] = Start;
        }
    }

    private void DisposeRefresher(TabId tabId)
    {
        if (refreshers.Remove(tabId, out var refresher))
        {
            refresher.Dispose();
        }
    }

    private void OnHostChanged()
    {
        StartActiveTabLoad();
        var activeId = host.Workspace.ActiveTabId;
        foreach (var (tabId, refresher) in refreshers)
        {
            refresher.SetActive(tabId == activeId);
        }
    }

    private void StartActiveTabLoad()
    {
        if (pendingLoads.Count > 0 && host.Workspace.ActiveTabId is { } active && pendingLoads.Remove(active, out var start))
        {
            start();
        }
    }
}
