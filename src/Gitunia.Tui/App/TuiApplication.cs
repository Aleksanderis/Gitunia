using Gitunia.Core.Clipboard;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Persistence;
using Gitunia.Core.Picker;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Discard;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Staging;
using Gitunia.Platform;
using Gitunia.Platform.ExternalTools;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Sidebar;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
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
public sealed class TuiApplication(GitServices services, ConsoleCapabilities capabilities, IPlatform platform) : IDisposable, ITerminalSuspender
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency; see docs/decisions.md.
    private const ushort IterationsPerSecond = 100;
    private const long OverlayDoubleClickGuardMilliseconds = 600;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    private IApplication _application = null!;
    private WorkspaceHost _host = null!;
    private EffectRunner _effects = null!;
    private BackgroundStatusPoller? _statusPoller;
    private MainView _view = null!;
    private ActionRouter _router = null!;
    private KeyController _keys = null!;
    private MouseController _mouse = null!;
    private long _overlayClickTicks = long.MinValue / 2;
    private VisualRangeController _visual = null!;
    private IReadOnlyList<IPromptHandler> _prompts = [];
    private SidebarActionHandler _sidebar = null!;
    private PaneKind _appliedFocus = PaneKind.Graph;
    private OverlayController _overlays = null!;
    private WhichKeyTimer _whichKeyTimer = null!;
    private PickerController _picker = null!;
    private DiffPaneController _diffPane = null!;
    private WorkingDiffController _workingDiff = null!;
    private WorkspaceActions _workspaceActions = null!;
    private GraphTabBinder _binder = null!;
    private string _homeDirectory = string.Empty;
    private readonly StatusChannel _statuses = new();
    private CommitActions _commits = null!;
    private SuspendableUiInvoker _uiInvoker = null!;
    private Action? _suspendedWork;
    private Action? _resumed;

    /// <summary>Runs the UI until the user quits.</summary>
    /// <param name="location">A repository to open on top of the restored session, null for none.</param>
    public int Run(RepositoryLocation? location)
    {
        var glyphs = GraphGlyphSet.For(capabilities.Unicode);
        _homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var store = new SessionStore(platform.StateDirectory);
        var restored = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        CreateApplication();
        _uiInvoker = new SuspendableUiInvoker(_application.Invoke);

        _host = new WorkspaceHost(Workspace.Empty);
        _effects = new EffectRunner(services, _host, InvokeOnUi, glyphs, _statuses);

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
            width => _diffPane.TitleLine(width, glyphs),
            new DiffListSource(glyphs));

        _commits = CreateCommits();
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
            clipboard);
        _view.WorkingDiffTitle = _workingDiff.TitleText;
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
        _router.DetailTabs = new DetailTabController(_host);
        _router.PaneSizes = new PaneSizeController(
            () => _diffPane.Arrangement(),
            () => _host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph,
            () => _view.ScreenSize,
            ApplyPaneSizes);
        _router.ExternalTools = new ExternalToolActions(
            new ExternalToolRunner(new ExternalToolLauncher(), this, _statuses, RefreshAfterTool, PauseForEnter),
            () => _host.Workspace.ActiveTab?.Location,
            SelectedCommitHash);
        _router.Refresh = () => _host.Workspace.ActiveTabId is { } activeId && _effects.Refresh(activeId);

        _picker = new PickerController(services.Discovery, _host, _homeDirectory, InvokeOnUi);
        _workspaceActions = new WorkspaceActions(_host, _picker, RequestQuit, _commits.HasUnsentMessage);

        _binder = new GraphTabBinder(
            _host,
            _effects,
            _view.Graph,
            new GraphListSource(new CommitGraph(), glyphs),
            _view.RefreshChrome);
        _binder.Switched += OnTabSwitched;
        _binder.GraphChanged += OnGraphChanged;

        var engine = new KeymapEngine(DefaultKeymap.Create());
        _keys = new KeyController(engine, RouteAction);
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
        _prompts = [_diffPane, _workingDiff, .. CreatePrompts()];
        _router.Prompts = _prompts;
        _keys.TextKeyHandler = key => _prompts.Any(prompt => prompt.HandleTextKey(key));

        // Subscribes itself to the host and the list in its constructor; nothing else needs the instance.
        _ = new SidebarPresenter(_host, _view.Sidebar, sidebarSource);

        // Same for the working-tree view; it must run before ApplyFocus, which needs the panes it shows.
        _ = new WorkingTreePresenter(_host, _view);

        _statuses.Changed += _view.RefreshChrome;
        _host.Changed += _view.RefreshChrome;
        _host.Changed += _binder.Sync;
        _host.Changed += ApplyFocus;
        _host.Changed += _commits.ForgetClosedTabs;
        _host.Changed += OnBottomTabMaybeChanged;
        _host.Changed += _visual.EndIfFocusMoved;
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
        _mouse = new MouseController(DefaultMouseMap.Create(), RouteAction, () => _overlays.State.IsOpen);
        _view.SuppressMouseWhen(() => _overlays.State.IsOpen);
        _view.Gestured += OnGestured;
        _view.MessageClicked += _commits.PlaceCursor;
        _view.Window.ViewportChanged += (_, _) => DeferToNextIteration(_diffPane.OnScreenResized);
        _view.Window.ViewportChanged += (_, _) => _overlays.OnScreenResized();
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
        _application.Keyboard.KeyDown += (_, key) => key.Handled = HandleKey(key);

        if (restored.Detail is { Length: > 0 } restoreNotice)
        {
            _statuses.Warning(restoreNotice);
        }

        _host.Restore(restored.Update);
        _commits.Restore(restored.Drafts);
        _statusPoller = new BackgroundStatusPoller(services.Status, _host, InvokeOnUi);
        _statusPoller.Start();
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
        _commits.DraftChanged += persistence.NotifyDraftChanged;
        persistence.Start();
        _view.Graph.SetFocus();
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
        _whichKeyTimer.Dispose();
        _statusPoller?.Dispose();
        _effects.Dispose();
        _view.Dispose();
    }

    private void RouteAction(ActionId action, int? count)
    {
        if (!_overlays.Handle(action, count) && !_workspaceActions.TryExecute(action, count))
        {
            _router.Execute(action, count);
        }
    }

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
        if (_overlays.State.IsOpen && gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            viewName = _overlays.ViewName;
        }

        // A click anywhere outside an open overlay closes it, like Esc.
        if (_overlays.State.IsOpen && gesture == MouseGesture.Click)
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
            or DefaultKeymap.DiffView
            or DefaultKeymap.WorkingUnstagedView
            or DefaultKeymap.WorkingStagedView
            or DefaultKeymap.WorkingUnstagedDiffView
            or DefaultKeymap.WorkingStagedDiffView;

    private void OnEdgeDragged(PaneEdge edge, int column, int row)
    {
        if (_overlays.State.IsOpen || _picker.IsOpen || ActiveSession()?.Diff is null || _diffPane.Arrangement().Zoomed is not null)
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
        if (_overlays.SelectPaletteEntryAt(line))
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(DefaultKeymap.PaletteView, MouseGesture.Click);
        }
        else if (_overlays.ConfirmAnswerAt(line) is { } answer)
        {
            _overlayClickTicks = Environment.TickCount64;
            _mouse.Handle(answer == ConfirmAnswer.Yes ? DefaultMouseMap.ConfirmYesButton : DefaultMouseMap.ConfirmNoButton, MouseGesture.Click);
        }
    }

    private void RefreshOverlay()
    {
        _view.Overlay.Show(_overlays.CurrentFrame());
        _view.RefreshChrome();
    }

    private PaneRects CurrentPaneRects()
    {
        var arrangement = _diffPane.Arrangement();
        return PaneLayout.Compute(_view.Window.Viewport.Width, _view.Window.Viewport.Height, arrangement.IsDiffOpen, arrangement.Zoomed, arrangement.Sizes);
    }

    private void Relayout()
    {
        _view.Window.SetNeedsLayout();
        _view.RefreshChrome();
    }

    // Unsent commit messages are saved with the session on exit and restored per tab, so a quit never loses one.
    private void RequestQuit() => RequestStop();

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

    private DiscardActions CreateDiscard(TabOperationGate gate) => new(
        _host,
        new PatchDiscarder(services.Diff, services.Discarder),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        ShowConfirm,
        gate);

    private StagingActions CreateStaging(TabOperationGate gate) => new(
        _host,
        services.IndexWriter,
        new PatchStager(services.Diff, services.IndexWriter),
        _statuses,
        InvokeOnUi,
        _effects.RefreshStatus,
        tabId => _effects.SessionOf(tabId)?.Cancellation.Token ?? CancellationToken.None,
        gate);

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
            FirstNotEmpty(_workspaceActions.Message, _router.Message, _statuses.Log.Latest?.Text ?? string.Empty),
            _view.Graph.SelectedItem,
            TabLabelFactory.Create(_host.Workspace, tabId => _effects.SessionOf(tabId) is { IsLoading: true, IsPending: false }),
            _picker.State,
            _homeDirectory)
        with
        {
            MessageSeverity = _workspaceActions.Message.Length + _router.Message.Length > 0 ? StatusSeverity.Info : _statuses.Log.Latest?.Severity ?? StatusSeverity.Info,
            Hints = _overlays.State.Kind == OverlayKind.Confirm
                ? PaneHints.ForConfirm()
                : _commits.IsEditing
                    ? PaneHints.ForMessageEditing()
                    : PaneHints.For(_host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph),
            SearchLine = _prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ?? string.Empty,
        };

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
            _application.Keyboard.KeyDown += (_, key) => key.Handled = HandleKey(key);
            _uiInvoker.Resume(_application.Invoke);
            if (resumed is not null)
            {
                _application.Invoke(resumed);
            }
        }
    }

    // The same setup for the first start and for every restart after a hand-off to an external tool.
    private void CreateApplication()
    {
        _application = Application.Create();
        _application.Init();
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

    private string? SelectedCommitHash()
    {
        var index = _view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var graph = ActiveSession()?.Graph;

        return graph is not null && index >= 0 && index < graph.Count ? graph[index].Id.ToString() : null;
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
        var palette = PaletteSettings.Choose(capabilities, driver is { SupportsTrueColor: true, Force16Colors: false });
        if (palette.Depth == ColorDepth.Ansi16 && driver is not null)
        {
            driver.Force16Colors = true;
        }

        Palette.Configure(palette);
    }

    private ClipboardCopier CreateClipboard()
    {
        var clipboardWriter = capabilities.IsInteractive
            ? new ClipboardWriter(Console.Out, Osc52Environment.Detect(Environment.GetEnvironmentVariable))
            : null;

        return new ClipboardCopier(clipboardWriter, _statuses);
    }

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
            () => Math.Max(0, GraphListSource.CommitIndexOf(_view.Graph.Viewport.Y)),
            () => _view.Graph.Viewport.Height,
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
            new QuickJumpController(handle),
            new RefJumpController(handle, () => _host.Workspace.ActiveTab?.Refs ?? []),
            new SearchController(handle, DeferToNextIteration),
            new CommitMessageInput(_commits),
        ];
    }

    private StyledLine DetailTitle(int width, GraphGlyphSet glyphs)
    {
        var content = ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;
        var isFocused = _view.Detail.HasFocus;
        return DetailPaneRenderer.RenderTitle(content, isFocused, width, glyphs);
    }

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
        }

        _visual.End();
        _view.Detail.Source = current?.DetailSource;
        _diffPane.OnTabSwitched(previous, current);
        _workingDiff.OnTabSwitched(previous, current);
        if (current is not null)
        {
            current.Details.Changed += RefreshDetail;
        }

        RefreshDetail();
    }

    private bool HandleKey(Key key)
    {
        var view = _picker.IsOpen && !_overlays.State.IsOpen ? DefaultKeymap.PickerView : _overlays.ViewName;
        return _keys.Handle(key, view) || (_picker.IsOpen && !_overlays.State.IsOpen);
    }

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

        if (record)
        {
            _host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(_view.Graph.Viewport.Y)));
        }
    }

    // The tab of the bottom pane is repository tab state: whoever changed it (key, click, restored session), the rows follow.
    private void OnBottomTabMaybeChanged()
    {
        if (ActiveSession() is { } session && _host.Workspace.ActiveTab is { } tab && session.DetailSource.Content.Tab != tab.View.BottomTab)
        {
            RefreshDetail();
        }
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
        var content = DetailContentBuilder.Build(entry, session.Details.Current, _host.Workspace.ActiveTab?.View.BottomTab ?? BottomTab.Changes);
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
        _view.Detail.EnsureSelectedItemVisible();
        _view.Detail.SetNeedsDraw();
        _view.RefreshChrome();
        _diffPane.FollowSelection();
    }
}
