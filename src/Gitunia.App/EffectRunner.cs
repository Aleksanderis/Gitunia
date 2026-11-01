using Gitunia.App.Background;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Loading.Diff;
using Gitunia.App.Loading.Stash;
using Gitunia.App.Loading.Trees;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.App.Tags;
using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Tags;
using Gitunia.Core.IssueTracker;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;

namespace Gitunia.App;

/// <summary>
/// Executes the effects the reducer returns and owns the per-tab sessions they create. A new feature adds one case
/// to <see cref="Run"/> that hands its effects to the feature's handler; results come back as commands through the
/// dispatcher. The switch ends in a throw, and a test checks that every concrete effect type has a case.
/// </summary>
/// <typeparam name="TSession">The session type the factory builds; the terminal layer adds its views to it.</typeparam>
public sealed class EffectRunner<TSession> : IDisposable
    where TSession : TabSession
{
    /// <summary>
    /// How many tabs, the active one included, keep their loaded graph; older tabs are evicted
    /// (docs/internal/performance.md, "Bounded memory").
    /// </summary>
    public const int DefaultWarmTabLimit = 5;

    private readonly GitServices _services;
    private readonly Dispatcher _host;
    private readonly IShellEffects _shell;
    private readonly TabCancellation _cancellation = new();
    private readonly BackgroundWork _background = new();
    private readonly TagEffectHandler _tags;
    private readonly Action<Action> _postToUi;
    private readonly StatusReporter _statuses;
    private readonly StatusAgingTimer _statusAging;
    private readonly ITabSessionFactory<TSession> _sessionFactory;
    private readonly TimeSpan _detailDebounce;
    private readonly Dictionary<TabId, TSession> _sessions = [];
    private readonly Dictionary<TabId, Action> _pendingLoads = [];
    private readonly Dictionary<TabId, TabRefresher> _refreshers = [];
    private readonly List<TabId> _recent = [];
    private readonly int _warmTabLimit;
    private readonly WatchBudget _watchBudget;
    private readonly RepositoryWatcherOptions _watcherOptions;
    private readonly PollOptions _pollOptions;

    public EffectRunner(
        GitServices services,
        Dispatcher host,
        Action<Action> postToUi,
        ITabSessionFactory<TSession> sessionFactory,
        StatusReporter statuses,
        TimeSpan? detailDebounce = null,
        RepositoryWatcherOptions? watcherOptions = null,
        WatchBudget? watchBudget = null,
        int warmTabLimit = DefaultWarmTabLimit,
        PollOptions? pollOptions = null,
        IShellEffects? shell = null)
    {
        _shell = shell ?? NoShellEffects.Instance;
        _tags = new TagEffectHandler(services.Tags, host);
        _pollOptions = pollOptions ?? PollOptions.Default;
        _warmTabLimit = Math.Max(1, warmTabLimit);
        _watcherOptions = watcherOptions ?? RepositoryWatcherOptions.Default;
        _watchBudget = watchBudget ?? new WatchBudget();
        _detailDebounce = detailDebounce ?? TimeSpan.FromMilliseconds(80);
        _services = services;
        _host = host;
        _postToUi = postToUi;
        _sessionFactory = sessionFactory;
        _statuses = statuses;
        _statusAging = new StatusAgingTimer(host);
        host.EffectRaised += Run;
        host.Changed += OnHostChanged;
    }

    /// <summary>
    /// Says whether a tab runs a write or remote operation; such a tab is never evicted
    /// (closing its session would cancel the work).
    /// </summary>
    public Func<TabId, bool> IsTabBusy { get; set; } = static _ => false;

    /// <summary>
    /// The bounded queue for background work (polls, prefetch, overview, issue links). Effects the user caused never wait
    /// in it.
    /// </summary>
    public BackgroundWork Background => _background;

    /// <summary>The sessions of the open tabs.</summary>
    public IEnumerable<TSession> Sessions => _sessions.Values;

    /// <summary>Cancels the running git work of every tab (a forced quit, #856).</summary>
    public void CancelAll()
    {
        _cancellation.CancelAll();
        foreach (var session in _sessions.Values)
        {
            session.Cancellation.Cancel(throwOnFirstException: false);
        }
    }

    /// <summary>The runtime session of an open tab, or null.</summary>
    public TSession? SessionOf(TabId tabId) => _sessions.GetValueOrDefault(tabId);

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
        _cancellation.Dispose();
        _statusAging.Dispose();
        _background.Dispose();
    }

    /// <summary>
    /// Performs one effect. An exception a handler lets escape becomes an <see cref="EffectFailedCommand"/> so state
    /// never stays "running"; an effect type without a case is a programming error and is not swallowed.
    /// </summary>
    public void Run(AppEffect effect)
    {
        try
        {
            Perform(effect);
        }
        catch (Exception exception) when (exception is not UnhandledEffectException)
        {
            var text = $"Internal error ({exception.GetType().Name}): {exception.Message}";
            _host.Dispatch(FailureOf(effect, text));
        }
    }

    // A feature's effect fails with that feature's result command, so its running flag is cleared.
    private static AppCommand FailureOf(AppEffect effect, string text) => effect switch
    {
        TagEffect tag => new TagOperationFailed(tag.TabId, text, null),
        _ => new EffectFailedCommand(text),
    };

    private void Perform(AppEffect effect)
    {
        switch (effect)
        {
            case CancelLoadEffect cancel:
                Cancel(cancel);
                break;
            case CopyToClipboardEffect copy:
                _shell.CopyToClipboard(copy);
                break;
            case LoadRepositoryEffect load:
                StartLoad(load);
                break;
            case OfferTerminalRetryEffect offer:
                _shell.OfferTerminalRetry(offer.TabId, offer.Noun, offer.Arguments);
                break;
            case PostStatusEffect post:
                _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
                break;
            case RefreshTabEffect refresh:
                Refresh(refresh.TabId, false);
                break;
            case RunActionEffect run:
                run.Run();
                break;
            case ScheduleStatusAgingEffect aging:
                _statusAging.Schedule(aging.After);
                break;
            case SetKeyModeEffect mode:
                _host.Dispatch(new SetKeyModeCommand(mode.Mode));
                break;
            case ShowConfirmEffect confirm:
                ShowConfirm(confirm);
                break;
            case TagEffect tag:
                _tags.Handle(tag, _cancellation.TokenOf(tag.TabId));
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

    private void ShowConfirm(ShowConfirmEffect confirm)
    {
        var refusal = new StatusPostedCommand(confirm.UnavailableNotice, StatusSeverity.Warning, _host.Clock.GetUtcNow());
        _host.Dispatch(new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal));
    }

    private void Cancel(CancelLoadEffect cancel)
    {
        _cancellation.Cancel(cancel.TabId);
        _pendingLoads.Remove(cancel.TabId);
        _recent.Remove(cancel.TabId);
        DisposeRefresher(cancel.TabId);
        if (_sessions.Remove(cancel.TabId, out var closed))
        {
            closed.Dispose();
        }
    }

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
                isBusy: () => IsBusy(load.TabId));
            _refreshers[load.TabId] = refresher;
            refresher.Start();
            refresher.SetActive(_host.Workspace.ActiveTabId == load.TabId);
            _ = Task.Run(() => QueueIssueLinksAsync(session, location, token), CancellationToken.None);
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
