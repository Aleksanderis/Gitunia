using System.Collections.Immutable;
using Gitunia.App.Settings;
using Gitunia.App.Shell;
using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.App.Loading.Commits;
using Gitunia.App.Tabs;
using Gitunia.App.Worktrees;
using Gitunia.Core.Shell;
using Gitunia.Core;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Keymap.Repeat;
using Gitunia.Core.Mouse;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Persistence;
using Gitunia.Core.Pick;
using Gitunia.Core.Picker;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Stash;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core.Worktrees;
using Gitunia.Git;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Platform;
using Gitunia.Platform.ExternalTools;
using Gitunia.Platform.Terminal;
using Gitunia.Platform.Urls;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Conflicts;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Prompts;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.App;

/// <summary>
/// Wires the Terminal.Gui shell to the state layer: keys go through the keymap engine, actions become workspace
/// commands, git results come back on the UI thread. This is the only place that knows both worlds.
/// </summary>
public sealed class TuiApplication(
    GitServices services,
    ConsoleCapabilities detectedCapabilities,
    IPlatform platform) : IDisposable, ITerminalSuspender, IShellEffects, IBranchSelectionSource,
    IGraphCommitSelector, IGraphSelectionSource, IPickSelectionSource,
    IStashSelectionSource, IStashViewHost, ITerminalRetryHost, IWorktreeSelectionSource
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency (docs/internal/history/decisions.md); the idle rate saves CPU (#909).
    private const ushort ActiveIterationsPerSecond = 100;
    private const ushort IdleIterationsPerSecond = 50;
    private static readonly TimeSpan IdleAfter = TimeSpan.FromSeconds(2);
    private const long OverlayDoubleClickGuardMilliseconds = 600;
    private const int HeadlessColumns = 120;
    private const int HeadlessRows = 40;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    private readonly IterationRateGovernor _iterationRate =
        new(ActiveIterationsPerSecond, IdleIterationsPerSecond, IdleAfter);
    private IApplication _application = null!;
    private Dispatcher _host = null!;
    private TerminalTabSessionFactory _sessionFactory = null!;
    private EffectRunner<TerminalTabSession> _effects = null!;
    private BackgroundStatusPoller? _statusPoller;
    private WorktreeOverviewPoller? _worktreePoller;
    private SidebarListSource? _sidebarSource;
    private MainView _view = null!;
    private ActionRouter _router = null!;
    private KeyController _keys = null!;
    private RepeatController _repeat = null!;
    private MouseController _mouse = null!;
    private long _overlayClickTicks = long.MinValue / 2;
    private VisualRangeController _visual = null!;
    private IReadOnlyList<IPromptHandler> _prompts = [];
    private SidebarActionHandler _sidebar = null!;
    private StashViewController _stashView = null!;
    private PaneKind _appliedFocus = PaneKind.Graph;
    private OverlayController _overlays = null!;
    private WhichKeyTimer _whichKeyTimer = null!;
    private PickerController _picker = null!;
    private DiffPaneController _diffPane = null!;
    private WorkingDiffController _workingDiff = null!;
    private QuickJumpController _quickJump = null!;
    private WorkspaceActions _workspaceActions = null!;
    private RepositoryCreationActions _creation = null!;
    private GraphTabBinder _binder = null!;
    private string _homeDirectory = string.Empty;
    private StatusReporter _statuses = null!;
    private readonly TabActivity _activity = new();
    private StartupConfig _startupConfig = null!;
    private ChipKeys _chipKeys = null!;
    private ConsoleCapabilities _capabilities = detectedCapabilities;
    private CommitActions _commits = null!;
    private TagActions _tags = null!;
    private PromptInputHandler _openPrompt = null!;
    private ClipboardCopier _clipboard = null!;
    private TerminalRetry _terminalRetry = null!;
    private ExternalToolRunner _externalToolRunner = null!;
    private PickActions _pick = null!;
    private PromptPopupTracker _popups = null!;
    private GraphGlyphSet _glyphs = null!;
    private FileTreeController _fileTree = null!;
    private ResetActions _reset = null!;
    private ReflogSession? _drawnReflog;
    private MainlineQuestion? _drawnMainline;
    private ScreenExtent? _drawnScreen;
    private SettingsSession? _drawnSettings;
    private ReflogInput _reflog = null!;
    private ReflogEntryStarter _reflogEntries = null!;
    private RebaseEditorController _rebaseEditor = null!;
    private ConflictEditorController _conflictEditor = null!;
    private SettingsScreenInput _settings = null!;
    private ConfigWriter _configWriter = null!;
    private ConfigEditorLauncher _configEditor = null!;
    private SessionPersistence? _persistence;
    private QuitGuard _quitGuard = null!;
    private SuspendableUiInvoker _uiInvoker = null!;
    private TerminalHandOff _terminalHandOff = null!;
    private Action? _suspendedWork;
    private Action? _resumed;

    /// <summary>Terminal.Gui driver to force (smoke tests); null picks the platform default.</summary>
    public string? DriverName { get; init; }

    /// <summary>Home directory for config paths and picker scans (smoke tests); null uses the user profile.</summary>
    public string? HomeDirectory { get; init; }

    /// <summary>Stop the main loop right after it starts, so a smoke test builds the shell and returns.</summary>
    public bool ExitAfterFirstIteration { get; init; }

    /// <summary>
    /// Smoke tests: receives the screen text after one forced draw in the first iteration (the loop stops right
    /// after). Only used with <see cref="ExitAfterFirstIteration"/>.
    /// </summary>
    public Action<string>? FrameCaptured { get; init; }

    /// <summary>Runs the UI until the user quits.</summary>
    /// <param name="location">A repository to open on top of the restored session, null for none.</param>
    public int Run(RepositoryLocation? location)
    {
        // First of all: the background query below makes .NET switch the terminal to no-echo, line-less input.
        _terminalHandOff = new TerminalHandOff(CaptureTerminalModes());
        _homeDirectory = HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _startupConfig = new ConfigStartup(platform.ConfigDirectory, _homeDirectory, ConfigStartup.DefaultLabelCaps(_capabilities)).Load();
        _chipKeys = new ChipKeys(_startupConfig.Keymap.Table);
        var queriedLight = default(bool?);
        if (_startupConfig.Theme == ThemeChoice.Auto)
        {
            queriedLight = platform.QueryLightBackground(_capabilities);
        }

        if (queriedLight is { } isLight)
        {
            _capabilities = _capabilities with { IsLightBackground = isLight };
        }

        var glyphs = GraphGlyphSet
            .For(_capabilities.Unicode)
            .WithLabelCaps(_startupConfig.Options.LabelCaps, _capabilities.Color != ColorDepth.None);
        _glyphs = glyphs;
        var store = new SessionStore(platform.StateDirectory);
        var restored = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        Application.MaximumIterationsPerSecond = ActiveIterationsPerSecond;
        CreateApplication();
        _uiInvoker = new SuspendableUiInvoker(_application.Invoke);

        _configWriter = new ConfigWriter(platform.ConfigDirectory);
        var settingsEnvironment = new SettingsEnvironment(
            _homeDirectory,
            Directory.Exists,
            _startupConfig.Themes.Names,
            ConfigStartup.DefaultLabelCaps(_capabilities));
        _host = new Dispatcher(WithSettings(settingsEnvironment), _uiInvoker);
        _statuses = new StatusReporter(_host);
        _sessionFactory = new TerminalTabSessionFactory(glyphs);
        _effects = new EffectRunner<TerminalTabSession>(
            services,
            _host,
            InvokeOnUi,
            _sessionFactory,
            _statuses,
            watcherOptions: WatcherOptionsFor(_startupConfig.Options),
            shell: this,
            settings: new SettingsServices(
                _configWriter,
                settingsEnvironment,
                SettingsRegistry.WithThemes(_startupConfig.Themes.Names),
                _startupConfig.Keymap.Table));

        _clipboard = CreateClipboard();
        var clipboard = _clipboard;
        var sidebarSource = new SidebarListSource(glyphs);
        _sidebarSource = sidebarSource;
        _sidebar = new SidebarActionHandler(
            _host,
            sidebarSource,
            ActiveSession,
            () => _view.Sidebar.Viewport.Height,
            () => !CurrentPaneRects().Sidebar.IsEmpty,
            () => !CurrentPaneRects().Detail.IsEmpty,
            SelectGraphItem,
            clipboard);
        _stashView = new StashViewController(_host, this, _statuses);
        _sidebar.StashView = _stashView;
        _host.Changed += _stashView.Reconcile;
        _host.Changed += RefreshOverlaysOnStateChange;
        _sidebar.IsGraphVisible = () => !CurrentPaneRects().Graph.IsEmpty;
        _sidebar.IsDiffVisible = () => !CurrentPaneRects().Diff.IsEmpty;

        _view = new MainView(
            CreateModel,
            width => DetailTitle(width, glyphs),
            glyphs,
            sidebarSource,
            () => _sidebar.Chrome,
            () => _diffPane.Arrangement(),
            width => _diffPane.TitleLine(width, glyphs, _chipKeys, _repeat.Active?.Pane == PaneKind.Diff),
            new DiffListSource(glyphs));

        _commits = CreateCommits();
        var branches = new BranchActions(_host, this);
        _tags = new TagActions(_host, this);
        _view.ActiveDraft = () => _host.Workspace.ActiveTabId is { } tabId ? _commits.DraftOf(tabId) : null;
        _view.IsEditingMessage = () => _commits.ShowsCursor;
        _router = new ActionRouter(
            _host,
            _view.Graph,
            _view.Detail,
            ActiveSession,
            RequestQuit,
            _sidebar,
            _statuses,
            clipboard);
        _router.ScreenHeight = () => _view.ScreenSize.Height;
        _diffPane = new DiffPaneController(
            _host,
            ActiveSession,
            _view.Detail,
            _view.Diff,
            () => _view.ScreenSize,
            Relayout,
            _view.RefreshChrome,
            clipboard);
        _router.Diff = _diffPane;
        _workingDiff = new WorkingDiffController(
            _host,
            ActiveSession,
            _view.WorkingDiff,
            _view.RefreshChrome,
            clipboard,
            Relayout);
        _view.WorkingDiffTitle = () => _workingDiff.TitleText(IsWorkingDiffRepeating());
        _view.IsWorkingDiffZoomed = () => _workingDiff.IsZoomed;
        _view.WorkingDiffButtons = () => WorkingTreePaneRenderer.StagingButtons(_host.Workspace.ActiveTab?.WorkingTree);
        _visual = new VisualRangeController(
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            PaneView,
            _host,
            new RangeCopier(clipboard, ActiveSession, () => _host.Workspace.ActiveTab?.WorkingTree).Copy,
            _statuses);
        _router.Visual = _visual;
        var operationGate = _activity.CreateGate();
        var staging = CreateStaging(operationGate);
        _commits.StageAllThen = staging.StageAllThen;
        _router.WorkingTree = new WorkingTreeActionHandler(_host, FocusedPageHeight, clipboard, staging, _commits)
        {
            Diff = _workingDiff,
            Visual = _visual,
            Discard = CreateDiscard(operationGate),
        };
        _router.Branches = branches;
        _router.Stash = new StashActions(_host, this);
        _router.Tags = _tags;
        _openPrompt = new PromptInputHandler(_host);
        _router.OpenPrompt = _openPrompt;
        // The tool runner comes before the remote actions, whose terminal retry launches git through it.
        _externalToolRunner = new ExternalToolRunner(
            new ExternalToolLauncher(),
            this,
            _statuses,
            RefreshAfterTool,
            PauseForEnter);

        _terminalRetry = new TerminalRetry(_host, _statuses, this);
        _effects.IsTabBusy = _activity.IsBusy;
        _router.Sync = new SyncActions(_host, this, _activity);
        _router.RemoteManagement = new RemoteManagementActions(_host, this);
        _reset = new ResetActions(_host, this);
        _router.Reset = _reset;
        _router.Merge = new MergeActions(_host, this);
        _router.Rebase = new RebaseActions(_host, this, this);
        _pick = new PickActions(_host, this);
        _router.Pick = _pick;
        _router.Compare = new CompareActions(
            _host,
            ActiveSession,
            () => SelectedCommitHash() is { } hash && ObjectId.TryParse(hash, out var id) ? id : null);
        _reflog = new ReflogInput(_host, this, glyphs);
        _reflogEntries = new ReflogEntryStarter(_host, _reset, this);
        _rebaseEditor = CreateRebaseEditor(glyphs, operationGate);
        _conflictEditor = CreateConflictEditor(glyphs, operationGate, staging);
        _settings = new SettingsScreenInput(_host, glyphs);
        _configEditor = new ConfigEditorLauncher(
            new ExternalToolLauncher(),
            this,
            _statuses,
            Environment.GetEnvironmentVariable,
            PauseForEnter);
        _router.Worktrees = new WorktreeActions(_host, this);
        _router.Operations = new OperationActions(_host);
        _router.DetailTabs = new DetailTabController(_host);
        _router.PaneSizes = new PaneSizeController(
            () => _diffPane.Arrangement(),
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            () => _view.ScreenSize,
            ApplyPaneSizes,
            () => _workingDiff.IsZoomed);
        _router.ExternalTools = new ExternalToolActions(
            _externalToolRunner,
            () => _host.Workspace.ActiveTab?.Location,
            SelectedCommitHash);
        _router.Refresh = () => _host.Workspace.ActiveTabId is { } activeId && _effects.Refresh(activeId);

        _picker = new PickerController(
            services.Discovery,
            _host,
            _homeDirectory,
            InvokeOnUi,
            moveDraft: _commits.MoveDraft,
            scanRoots: _startupConfig.Options.ScanRoots);
        _workspaceActions = new WorkspaceActions(_host, _picker, RequestQuit, _commits.HasUnsentMessage);

        _binder = new GraphTabBinder(
            _host,
            _effects,
            _view.Graph,
            new GraphListSource(new CommitGraph(), glyphs),
            _view.RefreshChrome);
        _binder.Switched += OnTabSwitched;
        _binder.GraphChanged += OnGraphChanged;

        var configured = _startupConfig.Keymap;
        var engine = new KeymapEngine(configured.Table);
        _keys = new KeyController(engine, RouteAction, _host);
        _repeat = new RepeatController(
            configured.Table,
            () => _host.Workspace.ActiveTabId,
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            () => IsAnyOverlayOpen() || _picker.IsOpen,
            RouteAction);
        _keys.Interceptor = _repeat.TryHandle;
        _fileTree = new FileTreeController(
            _host,
            ActiveSession,
            () => _view.Detail.SelectedItem,
            MoveDetailCursor,
            RefreshDetail,
            _diffPane.OpenFileContent);
        _router.FileTree = _fileTree;
        _router.IssueLinks = new IssueLinkActions(
            new UrlOpener(),
            clipboard,
            _statuses,
            () => ActiveSession()?.DetailSource,
            () => _view.Detail.SelectedItem,
            IsMessageFocused,
            InvokeOnUi);
        _whichKeyTimer = new WhichKeyTimer(OverlayController.WhichKeyDelay, InvokeOnUi);
        _overlays = new OverlayController(
            engine,
            _keys,
            _whichKeyTimer,
            RouteAction,
            () => _view.ScreenSize,
            glyphs,
            () => _router.ViewName,
            _statuses,
            _host);
        _overlays.WhichKeyEnabled = _startupConfig.Options.WhichKeyPopup;
        _overlays.Level = () => _startupConfig.Options.CommandLevel;
        _overlays.Changed += RefreshOverlay;
        _overlays.OperationInProgress = () => _host.Workspace.ActiveTab?.Operation is not null;
        _overlays.MenuTarget = view => MenuTargetOf(view, sidebarSource);
        _creation = new RepositoryCreationActions(
            services.Creator,
            _picker,
            _statuses,
            InvokeOnUi,
            _host,
            _view.RefreshChrome,
            _homeDirectory,
            DefaultCloneParent);
        _router.Creation = _creation;
        _quickJump = CreateQuickJump();
        _router.QuickJump = _quickJump;
        // The quick jump is also in the router (first in line, so Esc and Enter reach it in every pane) and here for its status line and typed keys.
        _prompts =
        [
            _creation,
            _diffPane,
            _workingDiff,
            _quickJump,
            _fileTree,
            .. CreatePrompts(),
        ];
        _router.Prompts = _prompts;
        _keys.TextKeyHandler = HandleTextKey;

        // Subscribes itself to the host and the list in its constructor; nothing else needs the instance.
        _ = new SidebarPresenter(_host, _view.Sidebar, sidebarSource);

        // Same for the Changes view; it must run before ApplyFocus, which needs the panes it shows.
        _ = new WorkingTreePresenter(_host, _view);

        _host.Changed += _view.RefreshChrome;
        _host.Changed += _binder.Sync;
        _host.Changed += ApplyFocus;
        _host.Changed += _commits.ForgetClosedTabs;
        _host.Changed += _commits.ShowPendingFailure;
        _host.Changed += OnBottomTabMaybeChanged;
        _host.Changed += _visual.EndIfFocusMoved;
        _host.Changed += _quickJump.CloseIfStale;
        _keys.PendingChanged += _view.RefreshChrome;
        _keys.TextKeyPressed += OnTextKey;
        _keys.TextKeyPressed += _sidebar.OnKey;
        _picker.Changed += OnPickerChanged;
        _view.Graph.ValueChanged += (_, _) => OnSelectionChanged();
        _view.Graph.HasFocusChanged += (_, _) => OnPaneFocused(_view.Graph, PaneKind.Graph);
        _view.Sidebar.HasFocusChanged += (_, _) => OnPaneFocused(_view.Sidebar, PaneKind.Sidebar);
        _view.Detail.HasFocusChanged += (_, _) => OnPaneFocused(_view.Detail, PaneKind.Detail);
        _view.Diff.HasFocusChanged += (_, _) => OnPaneFocused(_view.Diff, PaneKind.Diff);
        _view.WorkingUnstaged.HasFocusChanged += (_, _) => OnPaneFocused(_view.WorkingUnstaged, PaneKind.WorkingUnstaged);
        _view.WorkingStaged.HasFocusChanged += (_, _) => OnPaneFocused(_view.WorkingStaged, PaneKind.WorkingStaged);
        _view.WorkingDiff.HasFocusChanged += (_, _) => OnPaneFocused(_view.WorkingDiff, SelectedWorkingDiffPane());
        _view.WorkingMessage.HasFocusChanged += (_, _) => OnPaneFocused(_view.WorkingMessage, PaneKind.WorkingMessage);
        _mouse = new MouseController(DefaultMouseMap.Create(), RouteAction, IsAnyOverlayOpen);
        _view.SuppressMouseWhen(IsAnyOverlayOpen);
        _view.Gestured += OnGestured;
        _view.MessageClicked += _commits.PlaceCursor;
        _view.Window.ViewportChanged += (_, _) => DeferToNextIteration(_diffPane.OnScreenResized);
        _view.Window.ViewportChanged += (_, _) => ReportScreenSize();
        _view.Window.ViewportChanged += (_, _) => _overlays.OnScreenResized();
        _popups = new PromptPopupTracker(CurrentPopup, () => _view.ScreenSize, ShowOverlayFrame);
        _view.ChromeRefreshed += _popups.Sync;
        _view.Window.ViewportChanged += (_, _) => _popups.Sync();
        _view.EdgeDragged += OnEdgeDragged;
        _view.OverlayLineClicked += OnOverlayLineClicked;
        _view.OverlayLineDoubleClicked += OnOverlayLineDoubleClicked;
        _view.OverlayDragStarted += OnOverlayDragStarted;
        _view.OverlayDragged += _conflictEditor.OnDragged;
        _view.Graph.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.Detail.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.Detail.ValueChanged += (_, _) => _diffPane.OnDetailCursorMoved();
        _view.Diff.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingDiff.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingUnstaged.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingStaged.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.PickerRowClicked += OnPickerRowClicked;
        SubscribeInput();
        StartFocusReporting();

        if (restored.Detail is { Length: > 0 } restoreNotice)
        {
            _statuses.Warning(restoreNotice);
        }

        ConfigStartup.Report(configured.Problems, _statuses);

        _host.Restore(restored.Update);
        _commits.Restore(restored.Drafts);
        _statusPoller = new BackgroundStatusPoller(services.Status, _host, InvokeOnUi, background: _effects.Background);
        _statusPoller.Start();
        _worktreePoller = new WorktreeOverviewPoller(services.Status, _host, background: _effects.Background);
        _worktreePoller.Start();
        if (location is not null)
        {
            _host.Dispatch(new OpenRepositoryCommand(location));
        }

        _binder.Sync();
        if (_host.Workspace.Tabs.IsEmpty)
        {
            _picker.Open();
        }

        using var persistence = new SessionPersistence(store, _host, SaveDebounce, _statuses, InvokeOnUi, _commits.SnapshotForSession);
        _persistence = persistence;
        _quitGuard = new QuitGuard(_commits.HasAnyUnsentMessage, persistence.FlushSucceeded);
        _commits.DraftChanged += _quitGuard.Reset;
        _commits.DraftChanged += persistence.NotifyDraftChanged;
        persistence.Start();
        _view.Graph.SetFocus();
        if (ExitAfterFirstIteration)
        {
            StopAfterFirstFrame();
        }

        try
        {
            _application.Run(_view.Window);
            while (_suspendedWork is not null)
            {
                RunSuspended();
                _application.Run(_view.Window);
            }
        }
        finally
        {
            _statusPoller.Dispose();
            _worktreePoller.Dispose();
            persistence.Flush();
            StopFocusReporting();
            _application.Dispose();
        }

        return 0;
    }

    /// <summary>
    /// Stops the main loop; <see cref="Run"/> then shuts the driver down (the terminal is back in its normal mode, the
    /// input thread is gone), runs <paramref name="work"/> and starts the driver again, which redraws at the current size.
    /// </summary>
    public void Suspend(Action work, Action resumed)
    {
        _suspendedWork = work;
        _resumed = resumed;
        _uiInvoker.Suspend();
        _application.RequestStop();
    }

    public void Dispose()
    {
        _creation.CancelAndWait();
        _whichKeyTimer.Dispose();
        _statusPoller?.Dispose();
        _effects.Dispose();
        _view.Dispose();
    }

    private void RouteAction(ActionId action, int? count)
    {
        // The settings do not open over another overlay (help, palette, a dialog, the reflog).
        if ((action == ActionIds.SettingsOpen || action == ActionIds.SettingsOpenTheme)
            && (_overlays.State.IsOpen || _reflog.IsOpen || _rebaseEditor.IsOpen || _conflictEditor.IsOpen))
        {
            return;
        }

        // <space>em is global (#742): it must not start git mergetool under an open overlay or editor.
        if (action == ActionIds.ToolMergetool
            && (_overlays.State.IsOpen || _reflog.IsOpen || _rebaseEditor.IsOpen || _conflictEditor.IsOpen
                || _settings.IsOpen))
        {
            return;
        }

        if (_settings.TryExecute(action, count)
            || _conflictEditor.TryExecute(action, count)
            || _rebaseEditor.TryExecute(action, count)
            || _reflog.TryExecute(action, count))
        {
            return;
        }

        if (!_overlays.Handle(action, count)
            && !_creation.TryExecute(action, count)
            && !_workspaceActions.TryExecute(action, count))
        {
            _router.Execute(action, count);
            StartRepeat(action);
        }
    }

    private bool IsWorkingDiffRepeating() =>
        _repeat.Active?.Pane is PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff;

    // A step that took effect (the resize did not refuse, the context belongs to a diff) starts the sticky repeat mode.
    private void StartRepeat(ActionId action)
    {
        var isRefused = RepeatMode.IsResizeStep(action) && _router.PaneSizes?.Notice.Length > 0;
        var isHistoryDiff = _host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Diff;
        var hasNoDiffOptions = RepeatMode.IsContextStep(action)
            && isHistoryDiff
            && _view.Diff.Source is DiffListSource { Document.IsFileContent: true };
        if (!isRefused && !hasNoDiffOptions)
        {
            _repeat.Started(action);
        }
    }

    private bool HandleTextKey(KeyEvent key) =>
        _settings.HandleTextKey(key)
        || _rebaseEditor.HandleTextKey(key)
        || _conflictEditor.HandleTextKey(key)
        || _reflog.HandleTextKey(key)
        || _prompts.Any(prompt => prompt.HandleTextKey(key));

    private void OnGestured(string viewName, MouseGesture gesture, int? target)
    {
        // A double-click whose first click closed an overlay must not act on the pane that is now under the pointer.
        if (gesture == MouseGesture.DoubleClick && Environment.TickCount64 - _overlayClickTicks < OverlayDoubleClickGuardMilliseconds)
        {
            return;
        }

        // The sidebar and the message box have no ranges; dragging there only moves the selection.
        if (gesture == MouseGesture.DragStart
            && viewName is DefaultKeymap.SidebarView or DefaultKeymap.WorkingMessageView)
        {
            return;
        }

        // While an overlay is open the wheel scrolls it wherever the pointer is (other gestures on the panes behind it are ignored).
        if (IsAnyOverlayOpen() && gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            if (viewName == MainView.OverlayGestureView)
            {
                _settings.FocusColumnAt(_view.Overlay.PointerColumn);
            }

            viewName = OverlayViewName();
        }

        // A click anywhere outside an open overlay closes it, like Esc.
        if (IsAnyOverlayOpen() && gesture == MouseGesture.Click)
        {
            viewName = DefaultMouseMap.BackdropView;
            _overlayClickTicks = Environment.TickCount64;
        }

        // A drag starts a fresh range; a click on a row ends the range (the click moved the cursor, a position).
        if (gesture == MouseGesture.DragStart || (gesture is MouseGesture.Click or MouseGesture.DoubleClick && IsRangePane(viewName)))
        {
            _visual.End();
        }

        // The dirty marker belongs to a tab chip: the click first switches to that tab, then opens its working tree (#331).
        if (viewName == DefaultMouseMap.TabDirtyMarker)
        {
            _mouse.Handle(DefaultMouseMap.TabsView, gesture, target);
        }

        _mouse.Handle(viewName, gesture, target);

        // A key redraws the chrome through the pending-keys event; a mouse action has no such path (titles, status bar, mode).
        _view.RefreshChrome();
    }

    private static bool IsRangePane(string viewName) =>
        viewName is DefaultKeymap.GraphView
            or DefaultKeymap.DetailView
            or DefaultKeymap.DetailChangesView
            or DefaultKeymap.DetailFilesView
            or DefaultKeymap.DiffView
            or DefaultKeymap.WorkingUnstagedView
            or DefaultKeymap.WorkingStagedView
            or DefaultKeymap.WorkingUnstagedDiffView
            or DefaultKeymap.WorkingStagedDiffView;

    private void OnEdgeDragged(PaneEdge edge, int column, int row)
    {
        if (IsAnyOverlayOpen()
            || _picker.IsOpen
            || ActiveSession()?.Diff is null
            || _diffPane.Arrangement().Zoomed is not null)
        {
            return;
        }

        ApplyPaneSizes(PaneResizer.Drag(_diffPane.SizesOfActiveTab(), edge, column, row, _view.Window.Viewport.Height));
    }

    private void ApplyPaneSizes(PaneSizes sizes)
    {
        if (_host.Workspace.ActiveTabId is { } tabId && _diffPane.SizesOfActiveTab() != sizes)
        {
            _host.Dispatch(new TabCommand(tabId, new SetPaneSizesCommand(sizes.SidebarWidth, sizes.BottomHeight, sizes.DetailWidth)));
            Relayout();
        }
    }

    // Only the conflict editor uses a drag over an overlay: from a version row it starts a Visual selection.
    private void OnOverlayDragStarted(int line, int column)
    {
        if (_conflictEditor.OnDragStarted(line, column) is { } view)
        {
            _mouse.Handle(view, MouseGesture.DragStart);
        }
    }

    // Only the conflict editor uses the double-click of an overlay (a version row takes its version; a chip already ran
    // on the first click and does nothing again); the other overlays keep ignoring it.
    private void OnOverlayLineDoubleClicked(int line)
    {
        if (!_conflictEditor.IsOpen)
        {
            return;
        }

        var view = _conflictEditor.OnLineClicked(line, _view.Overlay.PointerColumn, out var handled, isDoubleClick: true);
        if (!handled)
        {
            return;
        }

        _overlayClickTicks = Environment.TickCount64;
        if (view is not null)
        {
            _mouse.Handle(view, MouseGesture.Click);
        }
    }

    // A click on a palette row highlights it and runs it (the palette is the menu of every action).
    private void OnOverlayLineClicked(int line)
    {
        var settingsView = _settings.OnLineClicked(line, _view.Overlay.PointerColumn, out var settingsHandled);
        var reflogView = _reflog.OnLineClicked(line, out var reflogHandled);
        var rebaseView = _rebaseEditor.OnLineClicked(line, _view.Overlay.PointerColumn, out var rebaseHandled);
        var conflictView = _conflictEditor.OnLineClicked(line, _view.Overlay.PointerColumn, out var conflictHandled);
        if (settingsHandled)
        {
            _overlayClickTicks = Environment.TickCount64;
            if (settingsView is not null)
            {
                _mouse.Handle(settingsView, MouseGesture.Click);
            }
        }
        else if (conflictHandled)
        {
            _overlayClickTicks = Environment.TickCount64;
            if (conflictView is not null)
            {
                _mouse.Handle(conflictView, MouseGesture.Click);
            }
        }
        else if (rebaseHandled)
        {
            _overlayClickTicks = Environment.TickCount64;
            if (rebaseView is not null)
            {
                _mouse.Handle(rebaseView, MouseGesture.Click);
            }
        }
        else if (reflogHandled)
        {
            _overlayClickTicks = Environment.TickCount64;
            if (reflogView is not null)
            {
                _mouse.Handle(reflogView, MouseGesture.Click);
            }
        }
        else if (_overlays.HelpTabNumberAt(line, _view.Overlay.PointerColumn) is { } helpTab)
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(DefaultMouseMap.HelpTabLabel, MouseGesture.Click, helpTab);
        }
        else if (_overlays.SelectPaletteEntryAt(line))
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(DefaultKeymap.PaletteView, MouseGesture.Click);
        }
        else if (_overlays.SelectMenuEntryAt(line))
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(DefaultKeymap.ContextMenuView, MouseGesture.Click);
        }
        else if (_pick.Prompt is { } prompt && !IsMainlineCovered())
        {
            if (MainlineFrameBuilder.ParentAt(line, prompt.Parents.Count) is { } parent)
            {
                _overlayClickTicks = Environment.TickCount64;
                _mouse.Handle(DefaultMouseMap.MainlineParentRow, MouseGesture.Click, parent);
            }
        }
        else if (_overlays.ConfirmAnswerAt(line) is { } answer)
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(answer == ConfirmAnswer.Yes ? DefaultMouseMap.ConfirmYesButton : DefaultMouseMap.ConfirmNoButton, MouseGesture.Click);
        }
    }

    private void RefreshOverlay()
    {
        ShowOverlayFrame();
        _view.RefreshChrome();
    }

    private void ShowOverlayFrame()
    {
        var overlay = _settings.CurrentFrame()
            ?? _conflictEditor.CurrentFrame()
            ?? _rebaseEditor.CurrentFrame()
            ?? _reflog.CurrentFrame()
            ?? _overlays.CurrentFrame();
        var frame = overlay ?? MainlineFrame() ?? PopupFrame();
        _popups.MarkShown(frame is not null);
        _view.Overlay.Show(frame);
    }

    private PromptPopup? CurrentPopup() =>
        _prompts.Select(prompt => prompt.Popup).FirstOrDefault(popup => popup is not null);

    // The popup of the open text or choice prompt, when no other overlay is open.
    private OverlayFrame? PopupFrame()
    {
        if (CurrentPopup() is not { } popup)
        {
            return null;
        }

        var size = _view.ScreenSize;
        return PromptPopupFrameBuilder.Build(popup, size.Width, size.Height, _glyphs);
    }

    // The status bar carries the prompt only when the screen is too small for its popup.
    private string PopupFallbackLine()
    {
        if (CurrentPopup() is not { } popup || IsMainlineCovered() || PopupFrame() is not null)
        {
            return string.Empty;
        }

        return popup.Input is { } input ? $"{popup.Title}: {input}_  {popup.Hint}" : $"{popup.Title}  {popup.Hint}";
    }

    // The mainline question of a cherry-pick or revert of a merge commit, when no other overlay is open.
    private OverlayFrame? MainlineFrame()
    {
        if (_pick.Prompt is not { } prompt)
        {
            return null;
        }

        var size = _view.ScreenSize;
        return MainlineFrameBuilder.Build(
            prompt.Title,
            prompt.Parents,
            size.Width,
            size.Height,
            _glyphs,
            prompt.UsableParents);
    }

    private PaneRects CurrentPaneRects()
    {
        var arrangement = _diffPane.Arrangement();
        return PaneLayout.Compute(
            _view.Window.Viewport.Width,
            _view.Window.Viewport.Height,
            arrangement.IsDiffOpen,
            arrangement.Zoomed,
            arrangement.Sizes,
            arrangement.ZoomBoth);
    }

    private void Relayout()
    {
        _view.Window.SetNeedsLayout();
        _view.RefreshChrome();
    }

    // Unsent commit messages are saved with the session on exit and restored per tab, so a quit never loses one.
    // Only when the session could not be saved does a quit guard remain: the first quit warns, the next one leaves (#369).
    private void RequestQuit() => TryQuit();

    private bool TryQuit()
    {
        if (_persistence is not null && !_quitGuard.ShouldQuit())
        {
            _statuses.Warning("Unsent commit message and the session could not be saved: quit again to leave and lose it");
            return false;
        }

        RequestStop();
        return true;
    }

    // Ctrl-c (#856) works in every mode and modal. The quit guard of a normal quit still applies (a second Ctrl-c leaves).
    // Running git work is cancelled only once the quit goes ahead, so a held-back Ctrl-c leaves the sessions usable;
    // Run's finally block saves the session and restores the terminal.
    private void ForceQuit()
    {
        if (TryQuit())
        {
            _effects.CancelAll();
        }
    }

    private CommitActions CreateCommits() => new(
        _host,
        services.CommitWriter,
        new RepositoryOperationDetector(),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        _activity.CreateGate(),
        () => _keys.Mode,
        RouteAction,
        _view.RefreshChrome);

    BranchSelection IBranchSelectionSource.Current() => BranchSelectionNow();

    void IShellEffects.StartReflogEntryAction(ReflogEntryActionEffect effect) => _reflogEntries.Start(effect);

    void IShellEffects.ApplyOptions(ConfigOptions options) => ApplyOptions(options);

    void IShellEffects.ApplyTheme(ThemeChoice theme) => ApplyTheme(theme);

    void IShellEffects.OpenConfigEditor()
    {
        var notice = _configEditor.Launch(_configWriter.FilePath, () => _host.Dispatch(new SettingsEditorClosed()));
        if (notice.Length > 0)
        {
            _statuses.Warning(notice);
        }
    }

    void IShellEffects.CopyToClipboard(CopyToClipboardEffect effect) =>
        _clipboard.Copy(effect.Text, effect.Description);

    void IShellEffects.OfferTerminalRetry(TabId tabId, string noun, IReadOnlyList<string> arguments) =>
        _terminalRetry.Offer(tabId, noun, arguments);

    bool ITerminalRetryHost.IsOverlayOpen() => IsAnyOverlayOpen();

    bool ITerminalRetryHost.IsBusy(TabId tabId) => _activity.IsBusy(tabId);

    string ITerminalRetryHost.RunGitInTerminal(string name, IReadOnlyList<string> arguments, RepositoryLocation location) =>
        _externalToolRunner.LaunchGit(name, arguments, location);

    // A focused sidebar acts on its ref, the graph on the Visual range when one is active, else the selected commit.
    PickSelection IPickSelectionSource.Current()
    {
        var chosen = BranchSelectionNow();
        if (chosen.RowKind is not null)
        {
            return new PickSelection(chosen.RowKind, chosen.CommitHash is { } hash ? [hash] : []);
        }

        var graph = ActiveSession()?.Graph;
        if (graph is null || _visual is not { IsActive: true, Range: { } range }
            || _host.Workspace.ActiveTab?.View.FocusedPane != PaneKind.Graph)
        {
            return new PickSelection(null, chosen.CommitHash is { } selected ? [selected] : []);
        }

        return new PickSelection(null, PickRange.Hashes(graph, range));
    }

    void IPickSelectionSource.EndRange() => _visual.End();

    string? IGraphSelectionSource.SelectedCommit() => SelectedCommitHash();

    // A focused sidebar acts on its row, any other pane on the commit selected in the graph.
    private BranchSelection BranchSelectionNow()
    {
        var inSidebar = _host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Sidebar;

        return BranchSelection.Of(inSidebar ? _sidebar.CursorRow : null, SelectedCommitHash);
    }

    // The sidebar row under the cursor (whichever pane has focus) and the commit selected in the graph.
    WorktreeTarget IWorktreeSelectionSource.Current()
    {
        var row = _sidebarSource is null ? null : SelectedSidebarRow(_sidebarSource);

        return new WorktreeTarget(row?.Kind, row?.Label, row?.Path, row?.CommitHash, SelectedCommitHash());
    }

    private DiscardActions CreateDiscard(TabOperationGate gate) => new(
        _host,
        new PatchDiscarder(services.Diff, services.Discarder),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        gate);


    // The actions menu of the row under the cursor of the graph or the sidebar, hanging at that row.
    private RowMenuTarget? MenuTargetOf(string view, SidebarListSource sidebarSource)
    {
        if (view == DefaultKeymap.DiffView)
        {
            if (_view.Diff.Source is DiffListSource { Document.IsFileContent: true })
            {
                return null;
            }

            return new RowMenuTarget(
                RowActions.ViewOptions,
                _view.ViewMenuAnchor(isWorking: false),
                RowActions.ViewOptionsTitle);
        }

        if (view == DefaultKeymap.WorkingUnstagedDiffView || view == DefaultKeymap.WorkingStagedDiffView)
        {
            return new RowMenuTarget(
                RowActions.ViewOptions,
                _view.ViewMenuAnchor(isWorking: true),
                RowActions.ViewOptionsTitle);
        }

        if (view == DefaultKeymap.DetailChangesView
            || view == DefaultKeymap.WorkingUnstagedView
            || view == DefaultKeymap.WorkingStagedView)
        {
            return new RowMenuTarget(
                RowActions.PathStyleOptions,
                _view.ListViewMenuAnchor(view),
                RowActions.ListViewOptionsTitle);
        }

        if (view == DefaultKeymap.DetailFilesView)
        {
            return new RowMenuTarget(
                RowActions.FilesTabViewOptions,
                _view.ListViewMenuAnchor(view),
                RowActions.ListViewOptionsTitle);
        }

        if (view == DefaultKeymap.SidebarView)
        {
            if (SelectedSidebarRow(sidebarSource) is { IsFolder: false } row)
            {
                return new RowMenuTarget(RowActions.OfSidebarRow(row), AnchorOf(_view.Sidebar));
            }

            return null;
        }

        if (view == DefaultKeymap.GraphView && GraphCommitHash() is not null)
        {
            return new RowMenuTarget(
                RowActions.OfGraphRowFor(
                    DetailLayout.PaneHeight(_view.ScreenSize.Height) > 0,
                    IsSelectedCommitRoot()),
                AnchorOf(_view.Graph));
        }

        return null;
    }

    private static MenuAnchor AnchorOf(PaneListView list)
    {
        var lastRow = Math.Max(0, list.Viewport.Height - 1);
        var visibleRow = Math.Clamp((list.SelectedItem ?? 0) - list.Viewport.Y, 0, lastRow);
        return new MenuAnchor(list.Frame.X, list.Frame.Y + visibleRow);
    }

    private SidebarRow? SelectedSidebarRow(SidebarListSource sidebarSource)
    {
        var rows = sidebarSource.Rows;
        if (_host.Workspace.ActiveTab is not { } tab || rows.IsEmpty)
        {
            return null;
        }

        return rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, rows.Length)];
    }

    private StagingActions CreateStaging(TabOperationGate gate) => new(
        _host,
        services.IndexWriter,
        new PatchStager(services.Diff, services.IndexWriter),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        gate);

    private TerminalTabSession? ActiveSession() => _host.Workspace.ActiveTabId is { } id ? _effects.SessionOf(id) : null;

    // The state decides which pane has focus; the toolkit focus follows it (a click goes the other way, see OnPaneFocused).
    private void ApplyFocus()
    {
        var wanted = _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        if (wanted == _appliedFocus)
        {
            return;
        }

        _appliedFocus = wanted;
        var pane = PaneView(wanted);
        if (!pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private int FocusedPageHeight() =>
        PaneView(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph).Viewport.Height;

    private PaneListView PaneView(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => _view.Sidebar,
        PaneKind.Detail => _view.Detail,
        PaneKind.Diff => _view.Diff,
        PaneKind.WorkingUnstaged => _view.WorkingUnstaged,
        PaneKind.WorkingStaged => _view.WorkingStaged,
        PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => _view.WorkingDiff,
        PaneKind.WorkingMessage => _view.WorkingMessage,
        _ => _view.Graph,
    };

    // A click gave a view the focus: record it in the state so the keymap uses the right view.
    private void OnPaneFocused(View pane, PaneKind kind)
    {
        // While panes are shown or hidden the toolkit moves the focus on its own; that is not the user's choice.
        var isWorkingPane = WorkingPanes.IsWorking(kind);
        var isWorkingView = _host.Workspace.ActiveTab?.View.Mode == TabViewMode.WorkingTree;
        var isUserChoice = isWorkingPane == isWorkingView && !_view.IsChangingVisibility;
        if (isUserChoice && pane.HasFocus && _host.Workspace.ActiveTabId is { } tabId
            && _host.Workspace.ActiveTab?.View.FocusedPane != kind)
        {
            _host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(kind)));
        }
    }

    // The diff pane of the side the selected file is on (a click on the diff box focuses that one).
    private PaneKind SelectedWorkingDiffPane() =>
        WorkingPanes.DiffOf(_host.Workspace.ActiveTab?.WorkingTree?.Selection?.Section ?? WorkingTreeSection.Unstaged);

    private void SelectGraphItem(int item)
    {
        if ((_view.Graph.Source?.Count ?? 0) > item)
        {
            _view.Graph.SelectedItem = item;
            _view.Graph.EnsureSelectedItemVisible();
        }
    }

    private ScreenModel CreateModel()
    {
        var screenModel = ScreenModelFactory.Create(
            _host.Workspace,
            ActiveSession(),
            _keys.Mode,
            _keys.PendingText,
            FirstNotEmpty(_workspaceActions.Message, _router.Message, RemoteProgressNow(), _statuses.Current?.Text ?? string.Empty),
            _view.Graph.SelectedItem,
            TabLabelFactory.Create(
                _host.Workspace,
                tabId => _effects.SessionOf(tabId) is { IsLoading: true, IsPending: false },
                _startupConfig.Options.ShowBranchInTab),
            _picker.State,
            _homeDirectory);

        var newScreenModel = screenModel
            with
            {
                RemoteBusy = RemoteIsBusy(),
                Keys = _chipKeys,
                SelectedName = FocusedSidebarName(),
                MessageSeverity = CurrentMessageSeverity(),
                MessageIsFresh = _statuses.IsFresh,
                Hints = CurrentHints(),
                RepeatPane = _repeat.Active?.Pane,
                SearchLine = _prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ??
                             PopupFallbackLine(),
            };

        return newScreenModel;
    }

    private StatusSeverity? CurrentMessageSeverity()
    {
        if (_workspaceActions.Message.Length + _router.Message.Length > 0 || RemoteIsBusy())
        {
            return null;
        }

        return _statuses.Current?.Severity;
    }

    private IReadOnlyList<KeyHint> ConflictEditorHints()
    {
        if (_conflictEditor.IsEditing)
        {
            return PaneHints.ForConflictEdit();
        }

        if (_conflictEditor.IsResultActive)
        {
            return PaneHints.ForConflictResult();
        }

        return _conflictEditor.IsVisual ? PaneHints.ForConflictVisual() : PaneHints.ForConflictVersions();
    }

    private IReadOnlyList<KeyHint> CurrentHints()
    {
        if (_overlays.State.Kind == OverlayKind.Confirm)
        {
            return PaneHints.ForConfirm();
        }

        if (_settings.IsOpen)
        {
            return PaneHints.ForSettings();
        }

        if (_conflictEditor.IsOpen)
        {
            return ConflictEditorHints();
        }

        if (_rebaseEditor.IsOpen)
        {
            return _rebaseEditor.IsEditingMessage ? PaneHints.ForRebaseMessage() : PaneHints.ForRebaseEditor();
        }

        if (_reflog.IsOpen)
        {
            return PaneHints.ForReflog();
        }

        if (_commits.IsEditing)
        {
            return PaneHints.ForMessageEditing();
        }

        if (_repeat.Active is { } repeating)
        {
            return PaneHints.ForRepeat(repeating.Kind);
        }

        // Dialogs, editors and the repeat mode above keep their hints: they say which keys act right now.
        return PaneHints.Limit(
            PaneAndConflictHints(),
            _startupConfig.Options.StatusBarHints,
            _startupConfig.Options.CommandLevel);
    }

    private IReadOnlyList<KeyHint> PaneAndConflictHints()
    {
        var hints = PaneHintsOfFocus();

        if (!ConflictPending())
        {
            return hints;
        }

        if (ConflictEditorSupport.AnyCanOpen(_host.Workspace.ActiveTab?.WorkingTree))
        {
            return PaneHints.WithConflictEditor(hints);
        }

        return PaneHints.WithConflictMergetool(hints);
    }

    private IReadOnlyList<KeyHint> PaneHintsOfFocus()
    {
        if (DetailChangesFocused())
        {
            return PaneHints.ForDetailChanges();
        }

        if (GraphComparing())
        {
            return PaneHints.ForGraphComparing();
        }

        return PaneHints.For(
            _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            _diffPane.IsShowingContent,
            OnConflictedFile(),
            OnRefusedConflictedFile());
    }

    // A merge, rebase, cherry-pick or revert is stopped on a conflict: <space>gr opens the editor from every pane.
    private bool ConflictPending() =>
        _host.Workspace.ActiveTab is { Operation: not null } tab
        && OperationBannerBuilder.HasConflicts(tab.WorkingTree);

    // The full name of the sidebar row under the cursor while the sidebar has focus (rows show short names).
    private string FocusedSidebarName()
    {
        if (_host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.Sidebar } tab
            && _view.Sidebar.Source is SidebarListSource { Rows.IsEmpty: false } source)
        {
            return SidebarCursorRow(source, tab)?.FullName ?? string.Empty;
        }

        return string.Empty;
    }

    // Only the Unstaged list and its diff lead with the conflict editor, so no other pane pays for the lookup.
    private bool OnConflictedFile() =>
        _host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && ConflictEditorSupport.CanOpen(entry.Conflict);

    // The same panes, on a conflicted file the editor refuses: the hints name the mergetool.
    private bool OnRefusedConflictedFile() =>
        _host.Workspace.ActiveTab is { View.FocusedPane: PaneKind.WorkingUnstaged or PaneKind.WorkingUnstagedDiff } tab
        && ConflictedSelection.Find(tab.WorkingTree) is { } entry
        && !ConflictEditorSupport.CanOpen(entry.Conflict);

    private bool GraphComparing() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Graph, CompareMark: not null };

    private bool DetailChangesFocused() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Changes };

    private string RemoteProgressNow() =>
        FirstNotEmpty(
            _host.Workspace.ActiveTab?.Sync?.ProgressLine ?? string.Empty,
            _creation.ProgressLine());

    private bool RemoteIsBusy() =>
        _creation.IsCloning || _host.Workspace.ActiveTab?.Sync is not null;

    // The folder a clone goes into when the picker holds no path: the first scan root that exists, else home.
    private string DefaultCloneParent()
    {
        var roots = _startupConfig.Options.ScanRoots
            ?? RepositoryScanner.DefaultRoots(_homeDirectory, Directory.Exists);
        return roots.FirstOrDefault(Directory.Exists) ?? _homeDirectory;
    }

    private static string FirstNotEmpty(params string[] texts) => texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;

    // Taken before the driver starts, while the terminal is as the shell left it.
    private static TerminalModes CaptureTerminalModes()
    {
        var modes = new TerminalModes();
        _ = modes.Capture();

        return modes;
    }

    private static void DoNothing()
    {
    }

    private void RunSuspended()
    {
        var work = _suspendedWork;
        var resumed = _resumed;
        _suspendedWork = null;
        _resumed = null;
        StopFocusReporting();
        _application.Dispose();
        try
        {
            _terminalHandOff.Run(work ?? DoNothing);
        }
        finally
        {
            CreateApplication();
            SubscribeInput();
            StartFocusReporting();
            _uiInvoker.Resume(_application.Invoke);
            if (resumed is not null)
            {
                _application.Invoke(resumed);
            }
        }
    }

    // Draws one frame inside the first loop iteration, hands the screen text to the test and stops the loop.
    private void StopAfterFirstFrame() => _application.Invoke(CaptureFirstFrameAndStop);

    private void CaptureFirstFrameAndStop()
    {
        EnsureHeadlessScreenSize();
        _application.LayoutAndDraw(true);
        FrameCaptured?.Invoke(_application.Driver?.ToString() ?? string.Empty);
        _application.RequestStop();
    }

    // The headless ANSI driver learns its size from a background poll and reports 0x0 until that lands, which under
    // load is after the first iteration (an empty frame, #628). A real terminal has its size from Init.
    private void EnsureHeadlessScreenSize()
    {
        if (_application.Driver is { Cols: 0 } or { Rows: 0 })
        {
            _application.Driver.SetScreenSize(HeadlessColumns, HeadlessRows);
        }
    }

    // The same setup for the first start and for every restart after a hand-off to an external tool.
    private void CreateApplication()
    {
        _application = Application.Create();
        _application.Init(DriverName);
        ConfigurePalette();
    }

    // The tool's own output (git's reason for failing) is on the screen; keep it there until a key is pressed.
    private static void PauseForEnter()
    {
        Console.Out.WriteLine();
        Console.Out.Write("Press Enter to return to gitunia...");
        Console.Out.Flush();
        Console.In.ReadLine();
    }

    private void RefreshAfterTool(RepositoryLocation location)
    {
        if (_host.Workspace.ActiveTabId is { } tabId && _host.Workspace.FindTab(tabId)?.Location == location)
        {
            _effects.Refresh(tabId, announce: false);
        }
    }

    // The overlays a mainline question or a prompt popup hides behind.
    private bool IsMainlineCovered() =>
        _overlays.State.IsOpen || _reflog.IsOpen || _settings.IsOpen || _rebaseEditor.IsOpen || _conflictEditor.IsOpen;

    private bool IsAnyOverlayOpen() => _rebaseEditor.IsOpen || _conflictEditor.IsOpen || IsOtherThanRebaseEditorOpen();

    // The rebase editor is covered by these (its Confirm above all) and must not take their keys.
    private bool IsOtherThanRebaseEditorOpen() =>
        _overlays.State.IsOpen
        || _reflog.IsOpen
        || _settings.IsOpen
        || _pick.Prompt is not null
        || CurrentPopup() is not null;

    // The conflict editor is covered by these (its Confirm above all) and must not take their keys.
    private bool IsOtherThanConflictEditorOpen() => _rebaseEditor.IsOpen || IsOtherThanRebaseEditorOpen();

    private string OverlayViewName()
    {
        if (_settings.IsOpen)
        {
            return DefaultKeymap.SettingsView;
        }

        if (_conflictEditor.IsOpen && !_overlays.State.IsOpen)
        {
            return _conflictEditor.ViewName;
        }

        if (_rebaseEditor.IsOpen && !_overlays.State.IsOpen)
        {
            return DefaultKeymap.RebaseEditorView;
        }

        return _reflog.IsOpen ? DefaultKeymap.ReflogView : _overlays.ViewName;
    }

    private static bool SameRoots(ImmutableArray<string>? current, ImmutableArray<string>? other)
    {
        if (current is null || other is null)
        {
            return current is null && other is null;
        }

        return current.Value.SequenceEqual(other.Value);
    }

    // Polling is the default; watching is opt-in and capped by the inotify instance limit (read at startup only).
    private static RepositoryWatcherOptions WatcherOptionsFor(ConfigOptions options)
    {
        var defaults = RepositoryWatcherOptions.Default;
        if (!options.WatchFiles)
        {
            return defaults;
        }

        var instances = InotifyLimit.ReadMaximumInstances();
        var cap = InotifyLimit.DirectoryCap(instances, defaults.MaximumWorkingTreeDirectories);

        return defaults with { UseFileWatchers = true, MaximumWorkingTreeDirectories = cap };
    }

    // The settings screen changed the file: the running app takes the new options at once (ux-settings.md, "Settings screen").
    private void ApplyOptions(ConfigOptions options)
    {
        _startupConfig = _startupConfig with { Options = options };
        ApplyLabelCaps(options.LabelCaps);
        _overlays.WhichKeyEnabled = options.WhichKeyPopup;
        if (!SameRoots(_picker.ScanRoots, options.ScanRoots))
        {
            _picker.ScanRoots = options.ScanRoots;
        }

        Relayout();
    }

    // The caps change the graph labels only: every tab's graph is redrawn with the set that has (or lacks) them.
    private void ApplyLabelCaps(LabelCapStyle style)
    {
        var glyphs = GraphGlyphSet.For(_capabilities.Unicode).WithLabelCaps(style, _capabilities.Color != ColorDepth.None);
        if (glyphs == _glyphs)
        {
            return;
        }

        _glyphs = glyphs;
        _sessionFactory.Glyphs = glyphs;
        foreach (var session in _effects.Sessions)
        {
            session.Source.Glyphs = glyphs;
        }

        _view.Graph.SetNeedsDraw();
    }

    // The theme on screen changed (previewed, kept or restored by the settings screen): the palette is rebuilt and
    // every view repainted. An auto theme that was not asked for at start uses the COLORFGBG guess; the terminal is
    // only queried (OSC 11) before the UI takes over the input.
    private void ApplyTheme(ThemeChoice theme)
    {
        if (_startupConfig.Theme == theme)
        {
            return;
        }

        _startupConfig = _startupConfig with { Theme = theme };
        ConfigurePalette();
        _view.RefreshPalette();
    }

    private RebaseEditorController CreateRebaseEditor(GraphGlyphSet glyphs, TabOperationGate gate) => new(
        _host,
        services.InteractiveRebase,
        services.CommitDetails,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        tabId => _effects.Refresh(tabId, false),
        BranchSelectionNow,
        IsOtherThanRebaseEditorOpen,
        () => _view.ScreenSize,
        glyphs,
        RefreshOverlay,
        gate,
        _chipKeys);

    private ConflictEditorController CreateConflictEditor(
        GraphGlyphSet glyphs,
        TabOperationGate gate,
        StagingActions staging) => new(
        _host,
        services.Conflicts,
        services.ConflictWriter,
        services.ConflictRecreator,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        staging.StageResolvedFor,
        tabId => _effects.Refresh(tabId, false),
        IsOtherThanConflictEditorOpen,
        () => _view.ScreenSize,
        glyphs,
        RefreshOverlay,
        gate,
        _chipKeys);

    // Moves the graph cursor to a commit; the reason it cannot is returned (empty on success).
    string IGraphCommitSelector.SelectCommit(TabId tabId, string hash)
    {
        var graph = _effects.SessionOf(tabId)?.Graph;
        if (graph is null || !ObjectId.TryParse(hash, out var id))
        {
            return "The graph is not loaded";
        }

        var index = graph.IndexOf(id);
        if (index < 0)
        {
            var shortHash = ShortHash.Of(hash);
            if (_effects.SessionOf(tabId)?.IsLoading == true)
            {
                return $"{shortHash} is not loaded yet";
            }

            return $"{shortHash} is not in the graph (it may be on no branch)";
        }

        SelectGraphItem(index + ScreenLayout.ReservedLeadingRows);
        return string.Empty;
    }

    private string? SelectedCommitHash() => _stashView.ActionTarget(GraphCommitHash());

    private string? GraphCommitHash()
    {
        var index = _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var graph = ActiveSession()?.Graph;

        return graph is not null && index >= 0 && index < graph.Count ? graph[index].Id.ToString() : null;
    }

    // The root commit has no parents, so no outgoing edge; its menu also offers the rebase of the whole history (#561).
    private bool IsSelectedCommitRoot()
    {
        var index = _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var graph = ActiveSession()?.Graph;

        return graph is not null
            && index >= 0
            && index < graph.Count
            && graph[index].Row.Edges.All(edge => edge.Kind != GraphEdgeKind.Outgoing);
    }

    // The shell state knows the screen size, so it can refuse a confirm dialog that does not fit and close one that no longer does.
    private void ReportScreenSize()
    {
        var size = _view.ScreenSize;
        _host.Dispatch(new ScreenResizedCommand(new ScreenExtent(size.Width, size.Height), _host.Clock.GetUtcNow()));
        _settings.OnScreenResized();
    }

    // The initial state: nothing open, and the machine facts the settings screen checks folders with.
    private static AppState WithSettings(SettingsEnvironment environment)
    {
        var settings = SettingsSession.Empty with { Environment = environment };

        return AppState.Empty with { Shell = ShellState.Empty with { Settings = settings } };
    }

    // The reflog list and the settings screen live in the shell state: a result that arrives later redraws them.
    private void RefreshOverlaysOnStateChange()
    {
        var shell = _host.State.Shell;
        if (ReferenceEquals(shell.Reflog, _drawnReflog) && ReferenceEquals(shell.Settings, _drawnSettings)
            && ReferenceEquals(shell.Mainline, _drawnMainline) && !MainlineNeedsLayout(shell))
        {
            return;
        }

        _drawnReflog = shell.Reflog;
        _drawnSettings = shell.Settings;
        _drawnMainline = shell.Mainline;
        _drawnScreen = shell.Screen;
        RefreshOverlay();
    }

    // An open mainline question is placed again when the window is resized.
    private bool MainlineNeedsLayout(ShellState shell) =>
        shell.Mainline is not null && !Equals(shell.Screen, _drawnScreen);

    private void InvokeOnUi(Action action) => _uiInvoker.Invoke(action);

    private void RequestStop() => _application.RequestStop();

    // Invoke runs at once when called on the UI thread; a zero timeout runs the work in a later loop iteration so keys are handled in between.
    private void DeferToNextIteration(Action work) => _application.AddTimeout(
        TimeSpan.Zero,
        () =>
        {
            work();
            return false;
        });

    private void ConfigurePalette()
    {
        var driver = _application.Driver;
        var palette = PaletteSettings
            .Choose(_capabilities, driver is { SupportsTrueColor: true, Force16Colors: false })
            .WithTheme(_startupConfig.Theme, _startupConfig.Themes);
        if (palette.Depth == ColorDepth.Ansi16 && driver is not null)
        {
            driver.Force16Colors = true;
        }

        Palette.Configure(palette);
    }

    private ClipboardCopier CreateClipboard()
    {
        var clipboardWriter = default(ClipboardWriter);
        if (_capabilities.IsInteractive)
        {
            var environment = Osc52Environment.Detect(Environment.GetEnvironmentVariable);
            clipboardWriter = new ClipboardWriter(Console.Out, environment);
        }

        return new ClipboardCopier(clipboardWriter, _statuses);
    }

    private static SidebarRow? SidebarCursorRow(SidebarListSource source, RepositoryState tab) =>
        source.Rows.IsEmpty ? null : source.Rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, source.Rows.Length)];

    // The file of the shown stash under the cursor of the detail pane's Changes list (restore file).
    private StashFileTarget? StashFileUnderCursor(RepositoryState tab)
    {
        if (tab.View.FocusedPane != PaneKind.Detail || ActiveSession() is not { } session
            || !_stashView.IsShowing)
        {
            return null;
        }

        return StashFileTargets.Resolve(
            session.StashView.Shown,
            session.DetailSource.Content,
            _view.Detail.SelectedItem ?? -1,
            session.Details.Current.Details?.UntrackedFileCount ?? 0);
    }

    string? IStashSelectionSource.CursorStashCommit() =>
        _sidebar.CursorRow is { Kind: SidebarRowKind.Stash } row ? row.CommitHash : null;

    StashFileTarget? IStashSelectionSource.FileUnderCursor() =>
        _host.Workspace.ActiveTab is { } tab ? StashFileUnderCursor(tab) : null;

    TerminalTabSession? IStashViewHost.ActiveSession() => ActiveSession();

    void IStashViewHost.ReselectGraphCommit() => ReselectGraphCommit();

    void IStashViewHost.RefreshDetail() => RefreshDetail();

    private IReadOnlyList<IPromptHandler> CreatePrompts()
    {
        var handle = new GraphViewHandle(
            ActiveSession,
            () => _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1,
            row =>
            {
                _view.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
                _view.Graph.EnsureSelectedItemVisible();
            },
            _host,
            () =>
            {
                _view.Graph.SetNeedsDraw();
                _view.RefreshChrome();
            });
        return
        [
            // The prompt that owns Insert mode must see Esc and Enter first, so the ref prompt comes before the search.
            _openPrompt,
            _pick,
            new RefJumpController(handle, () => _host.Workspace.ActiveTab?.Refs ?? []),
            new SearchController(
                handle,
                DeferToNextIteration,
                services.CommitMessages,
                tabId => _host.Workspace.FindTab(tabId)?.Location,
                InvokeOnUi),
            new CommitMessageInput(_commits),
        ];
    }

    private QuickJumpController CreateQuickJump() => new(new QuickJumpHost(
        ActiveSession,
        () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
        OpenJumpTarget,
        _host,
        _view.RefreshChrome));

    // The target of the focused list, null for a pane without a quick jump (the sidebar, the message box).
    private IJumpTarget? OpenJumpTarget()
    {
        if (ActiveSession() is not { } session)
        {
            return null;
        }

        return (_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph) switch
        {
            PaneKind.Graph => GraphJumpTarget(session),
            PaneKind.Diff => _diffPane.JumpTarget(),
            PaneKind.Detail => DetailJumpTarget(session),
            PaneKind.WorkingUnstaged => FileListJumpTarget(PaneKind.WorkingUnstaged, _view.UnstagedSource, _view.WorkingUnstaged),
            PaneKind.WorkingStaged => FileListJumpTarget(PaneKind.WorkingStaged, _view.StagedSource, _view.WorkingStaged),
            PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => _workingDiff.JumpTarget(),
            _ => null,
        };
    }

    private ListJumpTarget GraphJumpTarget(TerminalTabSession session) => new(
        PaneKind.Graph,
        text => session.Source.JumpLabels(_view.Graph, text),
        text =>
        {
            session.Source.JumpText = text;
            _view.Graph.SetNeedsDraw();
        },
        row =>
        {
            _view.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
            _view.Graph.EnsureSelectedItemVisible();
        });

    private ListJumpTarget DetailJumpTarget(TerminalTabSession session) => new(
        PaneKind.Detail,
        text => session.DetailSource.JumpLabels(_view.Detail, text),
        text =>
        {
            session.DetailSource.JumpText = text;
            _view.Detail.SetNeedsDraw();
        },
        row =>
        {
            _view.Detail.SelectedItem = row;
            _view.Detail.EnsureSelectedItemVisible();
        });

    private static ListJumpTarget FileListJumpTarget(PaneKind pane, WorkingTreeListSource source, PaneListView list) => new(
        pane,
        text => source.JumpLabels(list, text),
        text =>
        {
            source.JumpText = text;
            list.SetNeedsDraw();
        },
        row =>
        {
            list.SelectedItem = row;
            list.EnsureSelectedItemVisible();
        });

    private StyledLine DetailTitle(int width, GraphGlyphSet glyphs)
    {
        var content = ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;
        var isFocused = _view.Detail.HasFocus;
        return DetailPaneRenderer.RenderTitle(
            content,
            isFocused,
            width,
            glyphs,
            _chipKeys,
            _repeat.Active?.Pane == PaneKind.Detail);
    }

    private bool IsMessageFocused() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Details };

    private void OnGraphChanged()
    {
        foreach (var prompt in _prompts)
        {
            prompt.OnGraphChanged();
        }
    }

    private void OnTabSwitched(TerminalTabSession? previous, TerminalTabSession? current)
    {
        if (previous is not null)
        {
            previous.Details.Changed -= RefreshDetail;
            previous.Details.Changed -= _router.Compare!.Follow;
            previous.IssueLinksChanged -= RefreshDetail;
            if (previous.Tree is { IsEditingFilter: true } editing)
            {
                // A filter typed in the tab left behind must not keep owning Insert mode.
                editing.IsEditingFilter = false;
                _host.SetKeyMode(KeyMode.Normal);
            }

            if (previous.Tree is { } previousTree)
            {
                previousTree.Store.Changed -= RefreshDetail;
            }
        }

        _visual.End();
        _view.Detail.Source = current?.DetailSource;
        _diffPane.OnTabSwitched(previous, current);
        _workingDiff.OnTabSwitched(previous, current);
        if (current is not null)
        {
            current.Details.Changed += RefreshDetail;
            current.Details.Changed += _router.Compare!.Follow;
            current.IssueLinksChanged += RefreshDetail;
            if (current.Tree is { } currentTree)
            {
                currentTree.Store.Changed += RefreshDetail;
            }
        }

        RefreshDetail();
    }

    private bool HandleKey(Key key)
    {
        if (ForceQuitKey.IsMatch(key))
        {
            ForceQuit();
            return true;
        }

        var isPickerShowing = _picker.IsOpen && !IsAnyOverlayOpen();
        return _keys.Handle(key, FocusedViewName()) || isPickerShowing;
    }

    private void StartFocusReporting()
    {
        if (_application.Driver is { } driver)
        {
            TerminalFocusReporting.Start(driver, () => InvokeOnUi(_effects.CheckActiveTabNow));
        }
    }

    private void StopFocusReporting()
    {
        if (_application.Driver is { } driver)
        {
            TerminalFocusReporting.Stop(driver);
        }
    }

    private void SubscribeInput()
    {
        _application.Keyboard.KeyDown += (_, key) =>
        {
            _iterationRate.NoteInput();
            key.Handled = HandleKey(key);
        };
        _application.Paste += (_, paste) =>
        {
            _iterationRate.NoteInput();
            OnPaste(paste);
        };
        _application.Mouse.MouseEvent += (_, _) => _iterationRate.NoteInput();
        _application.Iteration += (_, _) =>
            Application.MaximumIterationsPerSecond = _iterationRate.CurrentRate();
    }

    /// <summary>
    /// A bracketed paste types its text, key by key, into whatever text input has Insert or Command mode; elsewhere it
    /// is ignored.
    /// </summary>
    private void OnPaste(PasteEventArgs paste)
    {
        if (string.IsNullOrEmpty(paste.Text))
        {
            return;
        }

        var multiLine = _commits.IsEditing || _rebaseEditor.IsEditingMessage || _conflictEditor.IsEditing;
        paste.Handled = _keys.Paste(paste.Text, multiLine, FocusedViewName);
    }

    private string FocusedViewName() =>
        _picker.IsOpen && !IsAnyOverlayOpen() ? DefaultKeymap.PickerView : OverlayViewName();

    private void OnTextKey(KeyEvent keyEvent)
    {
        if (!_picker.IsOpen || _keys.Mode != KeyMode.Insert)
        {
            return;
        }

        // The picker's query is plain state, not a text widget: it takes printable keys and edits itself with Backspace (some terminals send it as Ctrl-h).
        if (keyEvent.IsPrintable)
        {
            _picker.Apply(new TypeTextCommand(keyEvent.Name));
        }
        else if (keyEvent is { Name: KeyNames.Backspace, Modifiers: KeyModifiers.None or KeyModifiers.Ctrl })
        {
            _picker.Apply(new BackspaceCommand());
        }
    }

    private void OnPickerChanged()
    {
        if (_picker.IsOpen)
        {
            _visual.End();
        }

        if (!_overlays.State.IsOpen && _picker.IsOpen != (_keys.Mode == KeyMode.Insert))
        {
            _host.SetKeyMode(_picker.IsOpen ? KeyMode.Insert : KeyMode.Normal);
        }

        _view.ShowPicker(_picker.IsOpen);
        if (!_picker.IsOpen)
        {
            // Hiding the graph moved the toolkit focus: put it back on the pane the tab's state names.
            _appliedFocus = (PaneKind)(-1);
            ApplyFocus();
        }

        _view.RefreshChrome();
    }

    // A click highlights the row; the double-click's "open" comes from the mouse map as picker.accept.
    private void OnPickerRowClicked(int index)
    {
        if (_picker.State is { } state)
        {
            _picker.Apply(new MoveHighlightCommand(index - state.Highlight));
        }
    }

    private void OnSelectionChanged()
    {
        var session = ActiveSession();
        var tabId = _host.Workspace.ActiveTabId;
        if (session is null || tabId is null || _view.Graph.SelectedItem is not { } item)
        {
            return;
        }

        // While the binder swaps or restores the list, the tab's saved selection must not be overwritten, but the details still follow.
        var record = !_binder.IsSuspended;

        // A selection moved by the binder (reload, restore, tab switch) is not the user's choice (#716).
        var keepStash = _stashView.KeepsViewOnSelectionChange(_binder.IsSuspended || _binder.IsReplacing);
        var commitIndex = GraphListSource.CommitIndexOf(item);
        if (commitIndex >= 0 && commitIndex < session.Graph.Count)
        {
            var entry = session.Graph[commitIndex];
            if (record)
            {
                _host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(entry.Id)));
            }

            // Cheap on every move: the store shows "loading" at once and only asks git once the selection settles.
            if (!keepStash)
            {
                session.Details.Select(entry.Id, DetailPrefetchPlan.Around(session.Graph, commitIndex));
                RefreshDetail();
            }
        }
        else if (commitIndex >= session.Graph.Count && !keepStash)
        {
            // The continues-below marker holds no commit: the detail pane must not keep showing the previous one.
            RefreshDetail();
        }

        if (record)
        {
            _host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(_view.Graph.Viewport.Y)));
        }
    }

    // Ends a stash view: the pane goes back to the commit selected in the graph.
    private void ReselectGraphCommit()
    {
        var session = ActiveSession();
        if (session is not null && _view.Graph.SelectedItem is { } item)
        {
            var commitIndex = GraphListSource.CommitIndexOf(item);
            if (commitIndex >= 0 && commitIndex < session.Graph.Count)
            {
                session.Details.Select(
                    session.Graph[commitIndex].Id,
                    DetailPrefetchPlan.Around(session.Graph, commitIndex));
            }
        }

        RefreshDetail();
    }

    // The tab of the bottom pane is repository tab state: whoever changed it (key, click, restored session), the rows follow.
    private void OnBottomTabMaybeChanged()
    {
        if (ActiveSession() is { } session
            && _host.Workspace.ActiveTab is { } tab
            && (session.DetailSource.Content.Tab != tab.View.BottomTab
                || session.DetailSource.Content.PathStyle != tab.View.PathStyle))
        {
            RefreshDetail();
        }
    }

    private void MoveDetailCursor(int row)
    {
        if (row >= 0 && row < (_view.Detail.Source?.Count ?? 0))
        {
            _view.Detail.SelectedItem = row;
            _view.Detail.EnsureSelectedItemVisible();
        }
    }

    // The tree is only listed while the Files tab is shown, so scrolling the graph on another tab starts no ls-tree.
    private FileTreeView? FileTreeViewOf(TerminalTabSession session, GraphEntry? entry, BottomTab tab)
    {
        if (tab != BottomTab.Files || entry is null || session.Tree is not { } tree)
        {
            return null;
        }

        tree.Store.Select(entry.Id);

        return new FileTreeView(tree.Store.Current, tree.Rows, tree.Filter, SidebarGlyphs.For(_glyphs));
    }

    private void RefreshDetail()
    {
        var session = ActiveSession();
        if (session is null)
        {
            return;
        }

        var commitIndex = _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var stashName = StashViewController.ShownName(session);
        var entry = StashViewController.ShownEntry(session)
            ?? (commitIndex >= 0 && commitIndex < session.Graph.Count ? session.Graph[commitIndex] : null);
        var previous = session.DetailSource.Content;
        var tab = _host.Workspace.ActiveTab?.View.BottomTab ?? BottomTab.Changes;
        var content = DetailContentBuilder.Build(
            entry,
            session.Details.Current,
            tab,
            stashName is null ? FileTreeViewOf(session, entry, tab) : null,
            _host.Workspace.ActiveTab?.View.PathStyle ?? FilePathStyle.NameFirst,
            session.IssueLinks,
            session.CollapsedFolders,
            stashName);
        var previousCursor = _view.Detail.SelectedItem;
        session.DetailSource.Update(content);

        var landing = content.Tab == BottomTab.Changes && content.Files.Count > 0 ? content.FirstFileRow : 0;
        _view.Detail.SelectedItem = session.DetailCursor.Decide(new DetailCursorRequest(
            previous.ShortHash,
            content.ShortHash,
            previous.Tab,
            content.Tab,
            previous.Files.Count,
            landing,
            _view.Detail.SelectedItem,
            content.Rows.Count));
        var remapped = DetailCursorRemap.Row(previous, content, previousCursor);
        if (remapped >= 0)
        {
            _view.Detail.SelectedItem = remapped;
        }

        _view.Detail.EnsureSelectedItemVisible();
        _view.Detail.SetNeedsDraw();
        _view.RefreshChrome();
        _diffPane.FollowSelection();
        if (remapped >= 0)
        {
            // The cursor may keep its index while the row under it changed, so no cursor event tells the diff.
            _diffPane.OnDetailCursorMoved();
        }
    }
}
