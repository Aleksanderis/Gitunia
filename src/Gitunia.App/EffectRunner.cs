using Gitunia.Git.Discard;
using Gitunia.Git.Staging;
using Gitunia.App.Background;
using Gitunia.App.Branches;
using Gitunia.App.Commits;
using Gitunia.App.Compare;
using Gitunia.App.Conflicts;
using Gitunia.App.Creation;
using Gitunia.App.DiffPane;
using Gitunia.App.Discard;
using Gitunia.App.Pick;
using Gitunia.App.Rebase;
using Gitunia.App.Integration;
using Gitunia.App.IssueLinks;
using Gitunia.App.Picker;
using Gitunia.App.Reflog;
using Gitunia.App.Settings;
using Gitunia.App.Sidebar;
using Gitunia.App.Remotes;
using Gitunia.App.Staging;
using Gitunia.App.Stash;
using Gitunia.App.Status;
using Gitunia.App.Sync;
using Gitunia.App.Tabs;
using Gitunia.App.Tabs.Models;
using Gitunia.App.Tags;
using Gitunia.App.Worktrees;
using Gitunia.Core;
using Gitunia.Core.Branches;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Commits;
using Gitunia.Core.Compare;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Creation;
using Gitunia.Core.Details;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Discard;
using Gitunia.Core.Effects;
using Gitunia.Core.ExternalTools;
using Gitunia.Core.Input;
using Gitunia.Core.IssueLinks;
using Gitunia.Core.Jump;
using Gitunia.Core.Pick;
using Gitunia.Core.Integration;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Remotes;
using Gitunia.Core.Staging;
using Gitunia.Core.Stash;
using Gitunia.Core.Sync;
using Gitunia.Core.Tags;
using Gitunia.Core.Visual;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Platform.Urls;

namespace Gitunia.App;

/// <summary>
/// Executes the effects the reducer returns and owns the per-tab sessions they create. A new feature adds one case
/// to <see cref="Run"/> that hands its effects to the feature's handler; results come back as commands through the
/// dispatcher. The switch ends in a throw, and a test checks that every concrete effect type has a case.
/// </summary>
/// <typeparam name="TSession">The session type the factory builds; the terminal layer adds its views to it.</typeparam>
public sealed partial class EffectRunner<TSession> : IDisposable, ITabSessions, IStatusRefresher
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
    private readonly CreationEffectHandler _creation;
    private readonly DiffPaneEffectHandler _diffPane;
    private readonly DiscardEffectHandler _discard;
    private readonly IntegrationEffectHandler _integration;
    private readonly IssueLinkEffectHandler _issueLinks;
    private readonly BranchEffectHandler _branches;
    private readonly CommitEffectHandler _commits;
    private readonly CompareEffectHandler _compare;
    private readonly ConflictEffectHandler _conflicts;
    private readonly PickerEffectHandler _picker;
    private readonly PickEffectHandler _pick;
    private readonly RebaseEffectHandler _rebase;
    private readonly RebaseEditorEffectHandler _rebaseEditor;
    private readonly ReflogEffectHandler _reflog;
    private readonly SettingsEffectHandler? _settings;
    private readonly SidebarEffectHandler _sidebar;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly RemoteEffectHandler _remotes;
    private readonly StagingEffectHandler _staging;
    private readonly StashEffectHandler _stash;
    private readonly SyncEffectHandler _sync;
    private readonly TagEffectHandler _tags;
    private readonly WorktreeEffectHandler _worktrees;
    private readonly IUiInvoker _ui;
    private readonly WorkspaceBusySource _busy;
    private readonly StatusReporter _statuses;
    private readonly StatusAgingTimer _statusAging;
    private readonly ITabSessionFactory<TSession> _sessionFactory;
    private readonly TimeSpan _detailDebounce;
    private readonly TabSessionRegistry<TSession> _sessions;
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
        IUiInvoker ui,
        ITabSessionFactory<TSession> sessionFactory,
        EffectRunnerOptions<TSession>? options = null)
    {
        options ??= new EffectRunnerOptions<TSession>();
        _sessions = options.Sessions ?? new TabSessionRegistry<TSession>();
        var pickerServices = options.Picker ?? PickerServices.Default;
        var settings = options.Settings;
        _shell = options.Shell ?? NoShellEffects.Instance;
        _branches = new BranchEffectHandler(services.Branches, host);
        _commits = new CommitEffectHandler(services.CommitWriter, host);
        _compare = new CompareEffectHandler(this, host);
        _conflicts = new ConflictEffectHandler(
            services.Conflicts,
            services.ConflictWriter,
            services.ConflictRecreator,
            host);
        _creation = new CreationEffectHandler(services.Creator, pickerServices.Files, host);
        _diffPane = new DiffPaneEffectHandler(this);
        _discard = new DiscardEffectHandler(new PatchDiscarder(services.Diff, services.Discarder), this, host);
        _picker = new PickerEffectHandler(services.Discovery, pickerServices.Files, host, pickerServices.Timings);
        _pick = new PickEffectHandler(services.Pick, services.PickCommits, host);
        _rebase = new RebaseEffectHandler(services.Rebase, services.QuickRebase, host);
        _rebaseEditor = new RebaseEditorEffectHandler(services.InteractiveRebase, services.CommitDetails, host);
        _reflog = new ReflogEffectHandler(services.Reflog, host);
        _settings = settings is null ? null : new SettingsEffectHandler(settings, host);
        _sidebar = new SidebarEffectHandler(this, host);
        _integration = IntegrationEffectHandler.Create(services, host);
        _issueLinks = new IssueLinkEffectHandler(options.UrlOpener ?? new UrlOpener(), host);
        _remotes = new RemoteEffectHandler(services.RemoteManager, host);
        _staging = new StagingEffectHandler(
            services.IndexWriter,
            new PatchStager(services.Diff, services.IndexWriter),
            this,
            host);
        _stash = new StashEffectHandler(services.StashWriter, host);
        _sync = new SyncEffectHandler(services.Remote, services.RemoteManager, services.Ancestry, host);
        _tags = new TagEffectHandler(services.Tags, host);
        _worktrees = new WorktreeEffectHandler(services.Worktrees, services.Discovery, host);
        _pollOptions = options.Poll ?? PollOptions.Default;
        _warmTabLimit = Math.Max(1, options.WarmTabLimit);
        _watcherOptions = options.Watcher ?? RepositoryWatcherOptions.Default;
        _watchBudget = options.WatchBudget ?? new WatchBudget();
        _detailDebounce = options.DetailDebounce ?? TimeSpan.FromMilliseconds(80);
        _services = services;
        _host = host;
        _ui = ui;
        _busy = new WorkspaceBusySource(host);
        _sessionFactory = sessionFactory;
        _statuses = options.Statuses ?? new StatusReporter(host);
        _statusAging = new StatusAgingTimer(host);
        host.EffectRaised += Run;
        host.Changed += OnHostChanged;
    }

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

    TabSession? ITabSessions.Find(TabId tabId) => SessionOf(tabId);

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

    /// <inheritdoc />
    public void RefreshThen(TabId tabId, Action done)
    {
        _ui.Post(() =>
        {
            if (!RefreshStatus(tabId, done))
            {
                done();
            }
        });
    }

    public void Dispose()
    {
        foreach (var refresher in _refreshers.Values)
        {
            refresher.Dispose();
        }

        _refreshers.Clear();
        _shutdown.Cancel();
        _creation.CancelAndWait();
        _shutdown.Dispose();
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

    private void Perform(AppEffect effect)
    {
        switch (effect)
        {
            case BranchEffect branch:
                _branches.Handle(branch, _cancellation.TokenOf(branch.TabId));
                break;
            case CancelCommitEffect cancelCommit:
                _commits.Cancel(cancelCommit.TabId);
                break;
            case CancelLoadEffect cancel:
                Cancel(cancel);
                break;
            case CancelSyncEffect cancelSync:
                _sync.Cancel(cancelSync.TabId);
                break;
            case CommitReadEffect read:
                _commits.Handle(read, _cancellation.TokenOf(read.TabId));
                break;
            case CompareEffect compare:
                _compare.Handle(compare);
                break;
            case ConflictEffect conflict:
                _conflicts.Handle(conflict, _cancellation.TokenOf(conflict.TabId));
                break;
            case CopyToClipboardEffect copy:
                _shell.CopyToClipboard(copy);
                break;
            case CopyVisualRangeEffect copyRange:
                _shell.CopyVisualRange(copyRange);
                break;
            case CreationEffect creation:
                _creation.Handle(creation, _shutdown.Token);
                break;
            case DiffPaneEffect diffPane:
                _diffPane.Handle(diffPane);
                break;
            case DiscardEffect discard:
                _discard.Handle(discard, _cancellation.TokenOf(discard.TabId));
                break;
            case FlushSessionEffect flush:
                _host.Dispatch(new SessionFlushed(_shell.FlushSession(), flush.Force));
                break;
            case FollowStashCursorEffect:
                _shell.FollowStashCursor();
                break;
            case JumpToRowEffect jump:
                _shell.JumpToRow(jump.Row);
                break;
            case LaunchExternalToolEffect tool:
                _shell.LaunchExternalTool(tool);
                break;
            case IntegrationEffect integration:
                _integration.Handle(integration, _cancellation.TokenOf(integration.TabId));
                break;
            case LeaveStashEffect:
                _shell.LeaveStash();
                break;
            case LoadRepositoryEffect load:
                StartLoad(load);
                break;
            case MoveCommitDraftEffect move:
                _host.Dispatch(new CommitDraftMoveRequested(move.Missing, move.Target));
                break;
            case MoveDetailCursorEffect cursor:
                _shell.MoveDetailCursor(cursor.Row);
                break;
            case MoveDiffCursorEffect moveCursor:
                _shell.MoveDiffCursor(moveCursor);
                break;
            case OfferTerminalRetryEffect offer:
                _shell.OfferTerminalRetry(offer.TabId, offer.Noun, offer.Arguments);
                break;
            case OpenFileContentEffect open:
                _shell.OpenFileContent(open.Row);
                break;
            case OpenUrlEffect openUrl:
                _issueLinks.Handle(openUrl);
                break;
            case PickerEffect picker:
                _picker.Handle(picker, _shutdown.Token);
                break;
            case PickEffect pick:
                _pick.Handle(pick, _cancellation.TokenOf(pick.TabId));
                break;
            case PostStatusEffect post:
                _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
                break;
            case QuitEffect quit:
                if (quit.CancelWork)
                {
                    CancelAll();
                }

                _shell.Quit();
                break;
            case RouteActionEffect route:
                _shell.RouteAction(route);
                break;
            case RebaseEffect rebase:
                _rebase.Handle(rebase, _cancellation.TokenOf(rebase.TabId));
                break;
            case RebaseEditorEffect editor:
                _rebaseEditor.Handle(editor, _cancellation.TokenOf(editor.TabId));
                break;
            case ReadReflogEffect read:
                _reflog.Handle(read, _cancellation.TokenOf(read.TabId));
                break;
            case RefreshTabEffect refresh:
                Refresh(refresh.TabId, refresh.Announce);
                break;
            case ReflogEntryActionEffect entry:
                _shell.StartReflogEntryAction(entry);
                break;
            case RemoteEffect remote:
                _remotes.Handle(remote, _cancellation.TokenOf(remote.TabId));
                break;
            case RunCommitEffect commit:
                _commits.Handle(commit, _cancellation.TokenOf(commit.TabId));
                break;
            case RunActionEffect run:
                run.Run();
                break;
            case ScheduleStatusAgingEffect aging:
                _statusAging.Schedule(aging.After);
                break;
            case SettingsEffect settings:
                PerformSettings(settings);
                break;
            case SettingsShellEffect settingsShell:
                PerformSettingsShell(settingsShell);
                break;
            case SelectGraphIndexEffect select:
                _shell.SelectGraphIndex(select.Index);
                break;
            case SetKeyModeEffect mode:
                _host.Dispatch(new SetKeyModeCommand(mode.Mode));
                break;
            case ShowConfirmEffect confirm:
                ShowConfirm(confirm);
                break;
            case ShowPromptEffect prompt:
                _host.Dispatch(new PromptRequestedCommand(prompt.Request));
                break;
            case ShowStashEffect showStash:
                _shell.ShowStash(showStash.Row);
                break;
            case StageResolvedConflictEffect stage:
                _shell.StageResolvedConflict(stage);
                break;
            case StagingEffect staging:
                _staging.Handle(staging, _cancellation.TokenOf(staging.TabId));
                break;
            case StashEffect stash:
                _stash.Handle(stash, _cancellation.TokenOf(stash.TabId));
                break;
            case SyncEffect sync:
                _sync.Handle(sync, _cancellation.TokenOf(sync.TabId));
                break;
            case TagEffect tag:
                _tags.Handle(tag, _cancellation.TokenOf(tag.TabId));
                break;
            case LocateGraphCommitEffect locateCommit:
                _sidebar.Locate(locateCommit);
                break;
            case LocateWorktreeEffect locate:
                _worktrees.Locate(locate, _cancellation.TokenOf(locate.TabId));
                break;
            case WorktreeEffect worktree:
                _worktrees.Handle(worktree, _cancellation.TokenOf(worktree.TabId));
                break;
            default:
                throw new UnhandledEffectException(effect);
        }
    }

}
