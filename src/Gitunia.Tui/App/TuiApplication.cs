using System.Collections.Immutable;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Persistence;
using Gitunia.Core.Picker;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Gitunia.Platform;
using Gitunia.Platform.ExternalTools;
using Gitunia.Platform.Urls;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.Tags;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.App.Merge;
using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Pick;
using Gitunia.Tui.App.Reflog;
using Gitunia.Tui.App.Rebase;
using Gitunia.Tui.App.Reset;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Settings;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Operations;
using Gitunia.Tui.App.Stash;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.App.Worktrees;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Overlays;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
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
    IPlatform platform) : IDisposable, ITerminalSuspender
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency; see docs/decisions.md.
    private const ushort IterationsPerSecond = 100;
    private const long OverlayDoubleClickGuardMilliseconds = 600;
    private const int ShortHashLength = 7;
    private const int HeadlessColumns = 120;
    private const int HeadlessRows = 40;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    private IApplication _application = null!;
    private WorkspaceHost _host = null!;
    private EffectRunner _effects = null!;
    private BackgroundStatusPoller? _statusPoller;
    private WorktreeOverviewPoller? _worktreePoller;
    private MainView _view = null!;
    private ActionRouter _router = null!;
    private KeyController _keys = null!;
    private MouseController _mouse = null!;
    private long _overlayClickTicks = long.MinValue / 2;
    private long _statusAgingGeneration;
    private VisualRangeController _visual = null!;
    private IReadOnlyList<IPromptHandler> _prompts = [];
    private SidebarActionHandler _sidebar = null!;
    private WorktreeActions _worktrees = null!;
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
    private readonly StatusChannel _statuses = new();
    private StartupConfig _startupConfig = null!;
    private ChipKeys _chipKeys = null!;
    private ConsoleCapabilities _capabilities = detectedCapabilities;
    private CommitActions _commits = null!;
    private BranchActions _branches = null!;
    private TagActions _tags = null!;
    private RemoteActions _remote = null!;
    private ExternalToolRunner _externalToolRunner = null!;
    private PushActions _push = null!;
    private RemoteManagementActions _remoteManagement = null!;
    private PickActions _pick = null!;
    private MainlineOverlayTracker _mainline = null!;
    private PromptPopupTracker _popups = null!;
    private GraphGlyphSet _glyphs = null!;
    private FileTreeController _fileTree = null!;
    private ResetActions _reset = null!;
    private ReflogController _reflog = null!;
    private RebaseEditorController _rebaseEditor = null!;
    private SettingsController _settings = null!;
    private SessionPersistence? _persistence;
    private QuitGuard _quitGuard = null!;
    private SuspendableUiInvoker _uiInvoker = null!;
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
        _homeDirectory = HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _startupConfig = new ConfigStartup(platform.ConfigDirectory, _homeDirectory).Load();
        _chipKeys = new ChipKeys(_startupConfig.Keymap.Table);
        var queriedLight = _startupConfig.Theme == ThemeChoice.Auto
            ? platform.QueryLightBackground(_capabilities)
            : null;
        if (queriedLight is { } isLight)
        {
            _capabilities = _capabilities with { IsLightBackground = isLight };
        }

        var glyphs = GraphGlyphSet.For(_capabilities.Unicode);
        _glyphs = glyphs;
        var store = new SessionStore(platform.StateDirectory);
        var restored = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        CreateApplication();
        _uiInvoker = new SuspendableUiInvoker(_application.Invoke);

        _host = new WorkspaceHost(Workspace.Empty);
        _effects = new EffectRunner(
            services,
            _host,
            InvokeOnUi,
            glyphs,
            _statuses,
            watcherOptions: WatcherOptionsFor(_startupConfig.Options));

        var clipboard = CreateClipboard();
        var sidebarSource = new SidebarListSource(glyphs);
        _sidebar = new SidebarActionHandler(
            _host,
            sidebarSource,
            ActiveSession,
            () => _view.Sidebar.Viewport.Height,
            () => !CurrentPaneRects().Sidebar.IsEmpty,
            () => !CurrentPaneRects().Detail.IsEmpty,
            SelectGraphItem,
            mode => _keys.SetMode(mode),
            clipboard);
        _sidebar.IsGraphVisible = () => !CurrentPaneRects().Graph.IsEmpty;
        _sidebar.IsDiffVisible = () => !CurrentPaneRects().Diff.IsEmpty;
        _view = new MainView(
            CreateModel,
            width => DetailTitle(width, glyphs),
            glyphs,
            sidebarSource,
            () => _sidebar.Chrome,
            () => _diffPane.Arrangement(),
            width => _diffPane.TitleLine(width, glyphs, _chipKeys),
            new DiffListSource(glyphs));

        _commits = CreateCommits();
        _branches = CreateBranches();
        _tags = CreateTags();
        _view.ActiveDraft = () => _host.Workspace.ActiveTabId is { } tabId ? _commits.DraftOf(tabId) : null;
        _view.IsEditingMessage = () => _commits.IsEditing;
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
            mode => _keys.SetMode(mode),
            () => _view.ScreenSize,
            Relayout,
            _view.RefreshChrome,
            clipboard);
        _router.Diff = _diffPane;
        _workingDiff = new WorkingDiffController(
            _host,
            ActiveSession,
            _view.WorkingDiff,
            mode => _keys.SetMode(mode),
            _view.RefreshChrome,
            clipboard,
            Relayout);
        _view.WorkingDiffTitle = _workingDiff.TitleText;
        _view.IsWorkingDiffZoomed = () => _workingDiff.IsZoomed;
        _view.WorkingDiffButtons = () => WorkingTreePaneRenderer.StagingButtons(_host.Workspace.ActiveTab?.WorkingTree);
        _visual = new VisualRangeController(
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            PaneView,
            () => _keys.Mode,
            mode => _keys.SetMode(mode),
            new RangeCopier(clipboard, ActiveSession, () => _host.Workspace.ActiveTab?.WorkingTree).Copy);
        _router.Visual = _visual;
        var operationGate = new TabOperationGate();
        var staging = CreateStaging(operationGate);
        _commits.StageAllThen = staging.StageAllThen;
        _router.WorkingTree = new WorkingTreeActionHandler(_host, FocusedPageHeight, clipboard, staging, _commits)
        {
            Diff = _workingDiff,
            Visual = _visual,
            Discard = CreateDiscard(operationGate),
        };
        _router.Branches = _branches;
        _router.Tags = _tags;
        // The tool runner comes before the remote actions, whose terminal retry launches git through it.
        _externalToolRunner = new ExternalToolRunner(
            new ExternalToolLauncher(),
            this,
            _statuses,
            RefreshAfterTool,
            PauseForEnter);

        // The remote actions come first: remote management reads their running state through a method group.
        _remote = CreateRemote(operationGate);
        _effects.IsTabBusy = tabId => operationGate.IsBusy(tabId) || _remote.IsRunning(tabId);
        _router.Remote = _remote;
        _remoteManagement = CreateRemoteManagement();
        _router.RemoteManagement = _remoteManagement;
        _reset = CreateReset(operationGate);
        _router.Reset = _reset;
        _router.Merge = CreateMerge(operationGate);
        _router.Rebase = CreateRebase(operationGate);
        _pick = CreatePick(operationGate);
        _mainline = new MainlineOverlayTracker(
            () => _pick.Prompt is not null,
            IsMainlineCovered,
            RefreshOverlay);
        _router.Pick = _pick;
        _router.Compare = new CompareActions(
            _host,
            ActiveSession,
            () => SelectedCommitHash() is { } hash && ObjectId.TryParse(hash, out var id) ? id : null);
        _reflog = CreateReflog(glyphs);
        _rebaseEditor = CreateRebaseEditor(glyphs, operationGate);
        _settings = CreateSettings(glyphs);
        _worktrees = CreateWorktrees(operationGate, sidebarSource);
        _router.Worktrees = _worktrees;
        _router.WorktreeSwitch = CreateWorktreeSwitch(operationGate, sidebarSource);
        _worktrees.OpenWorktree = _router.WorktreeSwitch.OpenPath;
        _router.Operations = CreateOperations(operationGate);
        _push = CreatePush();
        _router.Push = _push;
        _router.DetailTabs = new DetailTabController(_host);
        _router.PaneSizes = new PaneSizeController(
            () => _diffPane.Arrangement(),
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            () => _view.ScreenSize,
            ApplyPaneSizes);
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
        _keys = new KeyController(engine, RouteAction);
        _fileTree = new FileTreeController(
            _host,
            ActiveSession,
            () => _view.Detail.SelectedItem,
            MoveDetailCursor,
            _keys.SetMode,
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
            _statuses);
        _overlays.Changed += RefreshOverlay;
        _overlays.OperationInProgress = () => _host.Workspace.ActiveTab?.Operation is not null;
        _overlays.MenuTarget = view => MenuTargetOf(view, sidebarSource);
        _creation = new RepositoryCreationActions(
            services.Creator,
            _picker,
            _statuses,
            InvokeOnUi,
            _keys.SetMode,
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
            _worktrees,
            .. CreatePrompts(operationGate, sidebarSource),
        ];
        _router.Prompts = _prompts;
        _keys.TextKeyHandler = HandleTextKey;

        // Subscribes itself to the host and the list in its constructor; nothing else needs the instance.
        _ = new SidebarPresenter(_host, _view.Sidebar, sidebarSource);

        // Same for the working-tree view; it must run before ApplyFocus, which needs the panes it shows.
        _ = new WorkingTreePresenter(_host, _view);

        _statuses.Changed += _view.RefreshChrome;
        _statuses.Changed += ScheduleStatusAging;
        _host.Changed += _view.RefreshChrome;
        _host.Changed += _mainline.Sync;
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
        _view.Window.ViewportChanged += (_, _) => _overlays.OnScreenResized();
        _view.Window.ViewportChanged += (_, _) => _settings.OnScreenResized();
        _mainline.Attach(_view.Window);
        _popups = new PromptPopupTracker(CurrentPopup, () => _view.ScreenSize, ShowOverlayFrame);
        _view.ChromeRefreshed += _popups.Sync;
        _view.Window.ViewportChanged += (_, _) => _popups.Sync();
        _view.EdgeDragged += OnEdgeDragged;
        _view.OverlayLineClicked += OnOverlayLineClicked;
        _view.Graph.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.Detail.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.Detail.ValueChanged += (_, _) => _diffPane.OnDetailCursorMoved();
        _view.Diff.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingDiff.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingUnstaged.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.WorkingStaged.ValueChanged += (_, _) => _visual.OnCursorMoved();
        _view.PickerRowClicked += OnPickerRowClicked;
        SubscribeInput();

        if (restored.Detail is { Length: > 0 } restoreNotice)
        {
            _statuses.Warning(restoreNotice);
        }

        ConfigStartup.Report(configured.Problems, _statuses);

        _host.Restore(restored.Update);
        _commits.Restore(restored.Drafts);
        _statusPoller = new BackgroundStatusPoller(services.Status, _host, InvokeOnUi);
        _statusPoller.Start();
        _worktreePoller = new WorktreeOverviewPoller(services.Status, _host, InvokeOnUi);
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
            && (_overlays.State.IsOpen || _reflog.IsOpen || _rebaseEditor.IsOpen))
        {
            return;
        }

        if (_settings.TryExecute(action, count)
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
        }
    }

    private bool HandleTextKey(KeyEvent key) =>
        _settings.HandleTextKey(key)
        || _rebaseEditor.HandleTextKey(key)
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

    // A click on a palette row highlights it and runs it (the palette is the menu of every action).
    private void OnOverlayLineClicked(int line)
    {
        var settingsView = _settings.OnLineClicked(line, _view.Overlay.PointerColumn, out var settingsHandled);
        var reflogView = _reflog.OnLineClicked(line, out var reflogHandled);
        var rebaseView = _rebaseEditor.OnLineClicked(line, _view.Overlay.PointerColumn, out var rebaseHandled);
        if (settingsHandled)
        {
            _overlayClickTicks = Environment.TickCount64;
            if (settingsView is not null)
            {
                _mouse.Handle(settingsView, MouseGesture.Click);
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
        _popups.MarkShown();
        var overlay = _settings.CurrentFrame()
            ?? _rebaseEditor.CurrentFrame()
            ?? _reflog.CurrentFrame()
            ?? _overlays.CurrentFrame();
        _view.Overlay.Show(overlay ?? MainlineFrame() ?? PopupFrame());
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
    private void RequestQuit()
    {
        if (_persistence is not null && !_quitGuard.ShouldQuit())
        {
            _statuses.Warning("Unsent commit message and the session could not be saved: quit again to leave and lose it");
            return;
        }

        RequestStop();
    }

    private CommitActions CreateCommits() => new(
        _host,
        services.CommitWriter,
        new RepositoryOperationDetector(),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowOutput,
        () => _keys.Mode,
        SetMode,
        RouteAction,
        _view.RefreshChrome,
        ShowConfirm);

    // The overlay controller and the key controller are created after the commit actions, so they are reached through these methods.
    private void ShowOutput(string title, string heading, IReadOnlyList<string> lines) =>
        _overlays.ShowOutput(title, heading, lines);

    private void SetMode(KeyMode mode) => _keys.SetMode(mode);

    private bool ShowConfirm(ConfirmPrompt prompt, Action onConfirmed) => _overlays.ShowConfirm(prompt, onConfirmed);

    private BranchActions CreateBranches() => new(
        _host,
        services.Branches,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SetMode,
        _view.RefreshChrome,
        BranchSelectionNow);

    private TagActions CreateTags() => new(
        _host,
        services.Tags,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SetMode,
        _view.RefreshChrome,
        BranchSelectionNow,
        OfferTerminalRetry);

    private void OfferTerminalRetry(
        TabId tabId,
        string noun,
        IReadOnlyList<string> arguments,
        Func<bool> isBusy) =>
        _remote.OfferTerminalRetry(tabId, noun, arguments, isBusy);

    private MergeActions CreateMerge(TabOperationGate gate) => new(
        _host,
        services.Merge,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        tabId => _effects.Refresh(tabId, false),
        BranchSelectionNow,
        gate);

    private PickActions CreatePick(TabOperationGate gate) => new(
        _host,
        services.Pick,
        services.PickCommits,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        tabId => _effects.Refresh(tabId, false),
#pragma warning disable IDE0200 // _keys is assigned after this runs; a method group would capture null
        mode => _keys.SetMode(mode),
#pragma warning restore IDE0200
        RefreshMainlineAndChrome,
        _visual.End,
        PickSelectionNow,
        gate);

    private void RefreshMainlineAndChrome()
    {
        _mainline.Sync();
        _view.RefreshChrome();
    }

    // A focused sidebar acts on its ref, the graph on the Visual range when one is active, else the selected commit.
    private PickSelection PickSelectionNow()
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

    private ResetActions CreateReset(TabOperationGate gate) => new(
        _host,
        services.Reset,
        services.Status,
        new RepositoryOperationDetector(),
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SetMode,
        _view.RefreshChrome,
        BranchSelectionNow,
        gate);

    private RebaseActions CreateRebase(TabOperationGate gate) => new(
        _host,
        services.Rebase,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        BranchSelectionNow,
        IsAnyOverlayOpen,
        gate);

    private QuickRebaseActions CreateQuickRebase(TabOperationGate gate) => new(
        _host,
        services.QuickRebase,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SelectedCommitHash,
        IsAnyOverlayOpen,
        SetMode,
        _view.RefreshChrome,
        gate);

    // A focused sidebar acts on its row, any other pane on the commit selected in the graph.
    private BranchSelection BranchSelectionNow()
    {
        var inSidebar = _host.Workspace.ActiveTab?.View.FocusedPane == PaneKind.Sidebar;
        if (inSidebar && _sidebar.CursorRow is { IsSection: false } row)
        {
            return new BranchSelection(row.Kind, row.Label, row.CommitHash);
        }

        return new BranchSelection(null, null, SelectedCommitHash());
    }

    private DiscardActions CreateDiscard(TabOperationGate gate) => new(
        _host,
        new PatchDiscarder(services.Diff, services.Discarder),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        gate);

    private RemoteManagementActions CreateRemoteManagement() => new(
        _host,
        services.RemoteManager,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SetMode,
        _view.RefreshChrome,
        BranchSelectionNow,
        _remote.IsRunning);

    private RemoteActions CreateRemote(TabOperationGate gate) => new(
        _host,
        services.Remote,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        tabId => _effects.Refresh(tabId, false),
        _view.RefreshChrome,
        gate,
        ShowConfirm,
        IsAnyOverlayOpen,
        _externalToolRunner.LaunchGit);

    private PushActions CreatePush() => new(
        _host,
        _remote,
        services.Remote,
        services.Tags.ListRemotesAsync,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        SetMode,
        _view.RefreshChrome,
        BranchSelectionNow);

    private OperationController CreateOperations(TabOperationGate gate) => new(
        _host,
        services.OperationWriter,
        _statuses,
        InvokeOnUi,
        tabId => _effects.Refresh(tabId, false),
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        gate);

    private WorktreeActions CreateWorktrees(TabOperationGate gate, SidebarListSource sidebarSource) => new(
        _host,
        services.Worktrees,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        tabId => _effects.Refresh(tabId, false),
        ShowConfirm,
        gate,
        () => SelectedSidebarRow(sidebarSource),
        SelectedCommitHash,
        SetMode,
        _view.RefreshChrome);

    private WorktreeSwitchActions CreateWorktreeSwitch(TabOperationGate gate, SidebarListSource sidebarSource) => new(
        _host,
        services.Discovery,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        gate,
        () => SelectedSidebarRow(sidebarSource));

    // The actions menu of the row under the cursor of the graph or the sidebar, hanging at that row.
    private RowMenuTarget? MenuTargetOf(string view, SidebarListSource sidebarSource)
    {
        if (view == DefaultKeymap.SidebarView)
        {
            return SelectedSidebarRow(sidebarSource) is { } row
                ? new RowMenuTarget(RowActions.OfSidebarRow(row), AnchorOf(_view.Sidebar))
                : null;
        }

        return view == DefaultKeymap.GraphView && SelectedCommitHash() is not null
            ? new RowMenuTarget(
                RowActions.OfGraphRowFor(
                    DetailLayout.PaneHeight(_view.ScreenSize.Height) > 0,
                    IsSelectedCommitRoot()),
                AnchorOf(_view.Graph))
            : null;
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
        gate,
        ShowConfirm);

    private TabSession? ActiveSession() => _host.Workspace.ActiveTabId is { } id ? _effects.SessionOf(id) : null;

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

    private ScreenModel CreateModel() =>
        ScreenModelFactory.Create(
            _host.Workspace,
            ActiveSession(),
            _keys.Mode,
            _keys.PendingText,
            FirstNotEmpty(
                _workspaceActions.Message,
                _router.Message,
                RemoteProgressNow(),
                _statuses.Current?.Text ?? string.Empty),
            _view.Graph.SelectedItem,
            TabLabelFactory.Create(
                _host.Workspace,
                tabId => _effects.SessionOf(tabId) is { IsLoading: true, IsPending: false },
                _startupConfig.Options.ShowBranchInTab),
            _picker.State,
            _homeDirectory)
        with
        {
            RemoteBusy = RemoteIsBusy(),
            Keys = _chipKeys,
            MessageSeverity = _workspaceActions.Message.Length + _router.Message.Length > 0 || RemoteIsBusy()
                ? null
                : _statuses.Current?.Severity,
            MessageIsFresh = _statuses.IsFresh,
            Hints = _overlays.State.Kind == OverlayKind.Confirm
                ? PaneHints.ForConfirm()
                : _settings.IsOpen
                    ? PaneHints.ForSettings()
                    : _rebaseEditor.IsOpen
                    ? (_rebaseEditor.IsEditingMessage ? PaneHints.ForRebaseMessage() : PaneHints.ForRebaseEditor())
                    : _reflog.IsOpen
                    ? PaneHints.ForReflog()
                    : _commits.IsEditing
                    ? PaneHints.ForMessageEditing()
                    : DetailChangesFocused()
                    ? PaneHints.ForDetailChanges()
                    : GraphComparing()
                    ? PaneHints.ForGraphComparing()
                    : PaneHints.For(
                        _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
                        _diffPane.IsShowingContent),
            SearchLine = _prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0)
                ?? PopupFallbackLine(),
        };

    private bool GraphComparing() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Graph, CompareMark: not null };

    private bool DetailChangesFocused() =>
        _host.Workspace.ActiveTab?.View is { FocusedPane: PaneKind.Detail, BottomTab: BottomTab.Changes };

    private string RemoteProgressNow() =>
        FirstNotEmpty(
            _host.Workspace.ActiveTabId is { } tabId ? _remote.ProgressLine(tabId) : string.Empty,
            _creation.ProgressLine());

    private bool RemoteIsBusy() =>
        _creation.IsCloning || (_host.Workspace.ActiveTabId is { } tabId && _remote.IsRunning(tabId));

    // The folder a clone goes into when the picker holds no path: the first scan root that exists, else home.
    private string DefaultCloneParent()
    {
        var roots = _startupConfig.Options.ScanRoots
            ?? RepositoryScanner.DefaultRoots(_homeDirectory, Directory.Exists);
        return roots.FirstOrDefault(Directory.Exists) ?? _homeDirectory;
    }

    private static string FirstNotEmpty(params string[] texts) => texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;

    private void RunSuspended()
    {
        var work = _suspendedWork;
        var resumed = _resumed;
        _suspendedWork = null;
        _resumed = null;
        _application.Dispose();
        try
        {
            work?.Invoke();
        }
        finally
        {
            CreateApplication();
            SubscribeInput();
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
        _overlays.State.IsOpen || _reflog.IsOpen || _settings.IsOpen || _rebaseEditor.IsOpen;

    private bool IsAnyOverlayOpen() => _rebaseEditor.IsOpen || IsOtherThanRebaseEditorOpen();

    // The rebase editor is covered by these (its Confirm above all) and must not take their keys.
    private bool IsOtherThanRebaseEditorOpen() =>
        _overlays.State.IsOpen
        || _reflog.IsOpen
        || _settings.IsOpen
        || _pick.Prompt is not null
        || CurrentPopup() is not null;

    private string OverlayViewName() => _settings.IsOpen
        ? DefaultKeymap.SettingsView
        : _rebaseEditor.IsOpen && !_overlays.State.IsOpen
            ? DefaultKeymap.RebaseEditorView
            : _reflog.IsOpen ? DefaultKeymap.ReflogView : _overlays.ViewName;

    private SettingsController CreateSettings(GraphGlyphSet glyphs) => new(
        new ConfigWriter(platform.ConfigDirectory),
        new SettingsEnvironment(_homeDirectory, Directory.Exists, _startupConfig.Themes.Names),
        _statuses,
        InvokeOnUi,
        () => _view.ScreenSize,
        glyphs,
        SetMode,
        RefreshOverlay,
        ApplyOptions,
        ApplyTheme,
        new ConfigEditorLauncher(
            new ExternalToolLauncher(),
            this,
            _statuses,
            Environment.GetEnvironmentVariable,
            PauseForEnter).Launch,
        () => KeyReferenceBuilder.Build(_startupConfig.Keymap.Table),
        SettingsRegistry.WithThemes(_startupConfig.Themes.Names));

    private static bool SameRoots(ImmutableArray<string>? current, ImmutableArray<string>? other) =>
        current is null || other is null
            ? current is null && other is null
            : current.Value.SequenceEqual(other.Value);

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

    // The settings screen changed the file: the running app takes the new options at once (ux.md, "Settings screen").
    private void ApplyOptions(ConfigOptions options)
    {
        _startupConfig = _startupConfig with { Options = options };
        if (!SameRoots(_picker.ScanRoots, options.ScanRoots))
        {
            _picker.ScanRoots = options.ScanRoots;
        }

        Relayout();
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
        ShowConfirm,
        tabId => _effects.Refresh(tabId, false),
        BranchSelectionNow,
        IsOtherThanRebaseEditorOpen,
        () => _view.ScreenSize,
        glyphs,
        SetMode,
        RefreshOverlay,
        gate);

    private ReflogController CreateReflog(GraphGlyphSet glyphs) => new(
        _host,
        services.Reflog,
        _reset,
        _branches,
        _statuses,
        InvokeOnUi,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        SelectCommitInGraph,
        BranchSelectionNow,
        () => _view.ScreenSize,
        glyphs,
        SetMode,
        RefreshOverlay);

    // Moves the graph cursor to a commit; the reason it cannot is returned (empty on success).
    private string SelectCommitInGraph(TabId tabId, string hash)
    {
        var graph = _effects.SessionOf(tabId)?.Graph;
        if (graph is null || !ObjectId.TryParse(hash, out var id))
        {
            return "The graph is not loaded";
        }

        var index = graph.IndexOf(id);
        if (index < 0)
        {
            var shortHash = hash[..Math.Min(ShortHashLength, hash.Length)];
            return _effects.SessionOf(tabId)?.IsLoading == true
                ? $"{shortHash} is not loaded yet"
                : $"{shortHash} is not in the graph (it may be on no branch)";
        }

        SelectGraphItem(index + ScreenLayout.ReservedLeadingRows);
        return string.Empty;
    }

    private string? SelectedCommitHash()
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

    // Added to the wait for a message to age, so the clock is surely past the threshold when the timeout fires.
    private static readonly TimeSpan StatusAgingMargin = TimeSpan.FromMilliseconds(20);

    // A message loses its highlight and then expires with no key pressed, so the bar is drawn again at those moments.
    // Each change of the channel replaces the earlier wait; a stale timeout does nothing.
    private void ScheduleStatusAging()
    {
        var generation = ++_statusAgingGeneration;
        if (_statuses.UntilNextChange is not { } wait)
        {
            return;
        }

        _application.AddTimeout(
            wait + StatusAgingMargin,
            () =>
            {
                if (generation == _statusAgingGeneration)
                {
                    _statuses.Tick();
                }

                return false;
            });
    }

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
        var clipboardWriter = _capabilities.IsInteractive
            ? new ClipboardWriter(Console.Out, Osc52Environment.Detect(Environment.GetEnvironmentVariable))
            : null;

        return new ClipboardCopier(clipboardWriter, _statuses);
    }

    private static SidebarRow? SidebarCursorRow(SidebarListSource source, RepositoryState tab) =>
        source.Rows.IsEmpty ? null : source.Rows[SidebarBuilder.ClampCursor(tab.Sidebar.Cursor, source.Rows.Length)];

    private IReadOnlyList<IPromptHandler> CreatePrompts(TabOperationGate gate, SidebarListSource sidebarSource)
    {
        var handle = new GraphViewHandle(
            ActiveSession,
            () => _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1,
            row =>
            {
                _view.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
                _view.Graph.EnsureSelectedItemVisible();
            },
            _keys.SetMode,
            () =>
            {
                _view.Graph.SetNeedsDraw();
                _view.RefreshChrome();
            });
        return
        [
            // The prompt that owns Insert mode must see Esc and Enter first, so the ref prompt comes before the search.
            new TabRenameController(_host, _keys.SetMode, _view.RefreshChrome),
            CreateQuickRebase(gate),
            _branches,
            _tags,
            _push,
            _remoteManagement,
            _reset,
            _pick,
            new RefJumpController(handle, () => _host.Workspace.ActiveTab?.Refs ?? []),
            new SearchController(
                handle,
                DeferToNextIteration,
                services.CommitMessages,
                tabId => _host.Workspace.FindTab(tabId)?.Location,
                InvokeOnUi),
            new CommitMessageInput(_commits),
            new StashController(
                _host,
                services.StashWriter,
                _statuses,
                InvokeOnUi,
                tabId => _effects.Refresh(tabId, false),
                tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
                ShowConfirm,
                tab => SidebarCursorRow(sidebarSource, tab),
                _keys.SetMode,
                _view.RefreshChrome,
                gate),
        ];
    }

    private QuickJumpController CreateQuickJump() => new(new QuickJumpHost(
        ActiveSession,
        () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
        OpenJumpTarget,
        _keys.SetMode,
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

    private ListJumpTarget GraphJumpTarget(TabSession session) => new(
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

    private ListJumpTarget DetailJumpTarget(TabSession session) => new(
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
        return DetailPaneRenderer.RenderTitle(content, isFocused, width, glyphs, _chipKeys);
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

    private void OnTabSwitched(TabSession? previous, TabSession? current)
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
                _keys.SetMode(KeyMode.Normal);
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
        var isPickerShowing = _picker.IsOpen && !IsAnyOverlayOpen();
        return _keys.Handle(key, FocusedViewName()) || isPickerShowing;
    }

    private void SubscribeInput()
    {
        _application.Keyboard.KeyDown += (_, key) => key.Handled = HandleKey(key);
        _application.Paste += (_, paste) => OnPaste(paste);
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

        var multiLine = _commits.IsEditing || _rebaseEditor.IsEditingMessage;
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
            _keys.SetMode(_picker.IsOpen ? KeyMode.Insert : KeyMode.Normal);
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
        var commitIndex = GraphListSource.CommitIndexOf(item);
        if (commitIndex >= 0 && commitIndex < session.Graph.Count)
        {
            var entry = session.Graph[commitIndex];
            if (record)
            {
                _host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(entry.Id)));
            }

            // Cheap on every move: the store shows "loading" at once and only asks git once the selection settles.
            session.Details.Select(entry.Id, DetailPrefetchPlan.Around(session.Graph, commitIndex));
            RefreshDetail();
        }
        else if (commitIndex >= session.Graph.Count)
        {
            // The continues-below marker holds no commit: the detail pane must not keep showing the previous one.
            RefreshDetail();
        }

        if (record)
        {
            _host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(_view.Graph.Viewport.Y)));
        }
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
    private FileTreeView? FileTreeViewOf(TabSession session, GraphEntry? entry, BottomTab tab)
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
        var entry = commitIndex >= 0 && commitIndex < session.Graph.Count ? session.Graph[commitIndex] : null;
        var previous = session.DetailSource.Content;
        var tab = _host.Workspace.ActiveTab?.View.BottomTab ?? BottomTab.Changes;
        var content = DetailContentBuilder.Build(
            entry,
            session.Details.Current,
            tab,
            FileTreeViewOf(session, entry, tab),
            _host.Workspace.ActiveTab?.View.PathStyle ?? FilePathStyle.NameFirst,
            session.IssueLinks,
            session.CollapsedFolders);
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
