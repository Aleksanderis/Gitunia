using Gitunia.Core.Clipboard;
using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Persistence;
using Gitunia.Core.Picker;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Platform;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Search;
using Gitunia.Tui.Views;
using Terminal.Gui.App;
using Terminal.Gui.Input;

namespace Gitunia.Tui.App;

/// <summary>
/// Wires the Terminal.Gui shell to the state layer: keys go through the keymap engine, actions become workspace
/// commands, git results come back on the UI thread. This is the only place that knows both worlds.
/// </summary>
public sealed class TuiApplication(GitRunner runner, ConsoleCapabilities capabilities, IPlatform platform) : IDisposable
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency; see docs/decisions.md.
    private const ushort IterationsPerSecond = 100;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);

    private WorkspaceHost host = null!;
    private EffectRunner effects = null!;
    private MainView view = null!;
    private ActionRouter router = null!;
    private KeyController keys = null!;
    private MouseController mouse = null!;
    private IReadOnlyList<IPromptHandler> prompts = [];
    private SidebarActionHandler sidebar = null!;
    private PaneKind appliedFocus = PaneKind.Graph;
    private OverlayController overlays = null!;
    private WhichKeyTimer whichKeyTimer = null!;
    private PickerController picker = null!;
    private DiffPaneController diffPane = null!;
    private WorkspaceActions workspaceActions = null!;
    private GraphTabBinder binder = null!;
    private string homeDirectory = string.Empty;
    private readonly StatusChannel statuses = new();

    /// <summary>Runs the UI until the user quits.</summary>
    /// <param name="location">A repository to open on top of the restored session, null for none.</param>
    public int Run(RepositoryLocation? location)
    {
        var glyphs = GraphGlyphSet.For(capabilities.Unicode);
        homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var store = new SessionStore(platform.StateDirectory);
        var restored = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        using var app = Application.Create();
        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        app.Init();

        host = new WorkspaceHost(Workspace.Empty);
        effects = new EffectRunner(runner, host, action => app.Invoke(action), glyphs, statuses);
        var clipboardWriter = capabilities.IsInteractive ? new ClipboardWriter(Console.Out, Osc52Environment.Detect(Environment.GetEnvironmentVariable)) : null;
        var clipboard = new ClipboardCopier(clipboardWriter, statuses);
        var sidebarSource = new SidebarListSource(glyphs);
        sidebar = new SidebarActionHandler(
            host,
            sidebarSource,
            ActiveSession,
            () => view.Sidebar.Viewport.Height,
            () => !CurrentPaneRects().Sidebar.IsEmpty,
            () => !CurrentPaneRects().Detail.IsEmpty,
            SelectGraphItem,
            mode => keys.SetMode(mode),
            clipboard);
        sidebar.IsGraphVisible = () => !CurrentPaneRects().Graph.IsEmpty;
        sidebar.IsDiffVisible = () => !CurrentPaneRects().Diff.IsEmpty;
        view = new MainView(
            CreateModel,
            width => DetailTitle(width, glyphs),
            glyphs,
            sidebarSource,
            () => sidebar.Chrome,
            () => diffPane.Arrangement(),
            width => diffPane.TitleLine(width, glyphs),
            new DiffListSource(glyphs),
            () => SidebarPane.Divider(view.Diff.HasFocus || view.Detail.HasFocus, glyphs));
        router = new ActionRouter(host, view.Graph, view.Detail, ActiveSession, app.RequestStop, sidebar, statuses, clipboard);
        diffPane = new DiffPaneController(host, ActiveSession, view.Detail, view.Diff, mode => keys.SetMode(mode), () => view.Window.Viewport.Height, Relayout, view.RefreshChrome, clipboard);
        router.Diff = diffPane;
        router.Refresh = () => host.Workspace.ActiveTabId is { } activeId && effects.Refresh(activeId);
        picker = new PickerController(runner, host, homeDirectory, action => app.Invoke(action));
        workspaceActions = new WorkspaceActions(host, picker, app.RequestStop);
        binder = new GraphTabBinder(host, effects, view.Graph, new GraphListSource(new CommitGraph(), glyphs), view.RefreshChrome);
        binder.Switched += OnTabSwitched;
        binder.GraphChanged += OnGraphChanged;
        var engine = new KeymapEngine(DefaultKeymap.Create());
        keys = new KeyController(engine, (action, count) => RouteAction(action, count));
        whichKeyTimer = new WhichKeyTimer(OverlayController.WhichKeyDelay, action => app.Invoke(action));
        overlays = new OverlayController(engine, keys, whichKeyTimer, RouteAction, () => view.ScreenSize, glyphs, () => router.ViewName, statuses);
        overlays.Changed += RefreshOverlay;
        prompts = [diffPane, .. CreatePrompts(app)];
        router.Prompts = prompts;
        keys.TextKeyHandler = key => prompts.Any(prompt => prompt.HandleTextKey(key));
        var presenter = new SidebarPresenter(host, view.Sidebar, sidebarSource);

        statuses.Changed += view.RefreshChrome;
        host.Changed += view.RefreshChrome;
        host.Changed += binder.Sync;
        host.Changed += ApplyFocus;
        keys.PendingChanged += view.RefreshChrome;
        keys.TextKeyPressed += OnTextKey;
        keys.TextKeyPressed += sidebar.OnKey;
        picker.Changed += OnPickerChanged;
        view.Graph.ValueChanged += (_, _) => OnSelectionChanged();
        view.Graph.HasFocusChanged += (_, _) => OnPaneFocused(view.Graph, PaneKind.Graph);
        view.Sidebar.HasFocusChanged += (_, _) => OnPaneFocused(view.Sidebar, PaneKind.Sidebar);
        view.Detail.HasFocusChanged += (_, _) => OnPaneFocused(view.Detail, PaneKind.Detail);
        view.Diff.HasFocusChanged += (_, _) => OnPaneFocused(view.Diff, PaneKind.Diff);
        mouse = new MouseController(DefaultMouseMap.Create(), RouteAction, () => overlays.State.IsOpen);
        view.SuppressMouseWhen(() => overlays.State.IsOpen);
        view.Gestured += (viewName, gesture, target) => mouse.Handle(viewName, gesture, target);
        view.PickerRowClicked += OnPickerRowClicked;
        app.Keyboard.KeyDown += (_, key) => key.Handled = HandleKey(key);

        if (restored.Detail is { Length: > 0 } restoreNotice)
        {
            statuses.Warning(restoreNotice);
        }

        host.Restore(restored.Update);
        if (location is not null)
        {
            host.Dispatch(new OpenRepositoryCommand(location));
        }

        binder.Sync();
        if (host.Workspace.Tabs.IsEmpty)
        {
            picker.Open();
        }

        using var persistence = new SessionPersistence(store, host, SaveDebounce, statuses, action => app.Invoke(action));
        persistence.Start();
        view.Graph.SetFocus();
        try
        {
            app.Run(view.Window);
        }
        finally
        {
            persistence.Flush();
        }

        return 0;
    }

    public void Dispose()
    {
        whichKeyTimer?.Dispose();
        effects?.Dispose();
        view?.Dispose();
    }

    private void RouteAction(ActionId action, int? count)
    {
        if (!overlays.Handle(action, count) && !workspaceActions.TryExecute(action, count))
        {
            router.Execute(action, count);
        }
    }

    private void RefreshOverlay()
    {
        view.Overlay.Show(overlays.CurrentFrame());
        view.RefreshChrome();
    }

    private PaneRects CurrentPaneRects()
    {
        var arrangement = diffPane.Arrangement();
        return PaneLayout.Compute(view.Window.Viewport.Width, view.Window.Viewport.Height, arrangement.IsDiffOpen, arrangement.Zoomed);
    }

    private void Relayout()
    {
        view.Window.SetNeedsLayout();
        view.RefreshChrome();
    }

    private TabSession? ActiveSession() => host.Workspace.ActiveTabId is { } id ? effects.SessionOf(id) : null;

    // The state decides which pane has focus; the toolkit focus follows it (a click goes the other way, see OnPaneFocused).
    private void ApplyFocus()
    {
        var wanted = host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        if (wanted == appliedFocus)
        {
            return;
        }

        appliedFocus = wanted;
        var pane = wanted switch
        {
            PaneKind.Sidebar => view.Sidebar,
            PaneKind.Detail => view.Detail,
            PaneKind.Diff => view.Diff,
            _ => view.Graph,
        };
        if (!pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    // A click gave a view the focus: record it in the state so the keymap uses the right view.
    private void OnPaneFocused(Terminal.Gui.ViewBase.View pane, PaneKind kind)
    {
        if (pane.HasFocus && host.Workspace.ActiveTabId is { } tabId && host.Workspace.ActiveTab?.View.FocusedPane != kind)
        {
            host.Dispatch(new TabCommand(tabId, new FocusPaneCommand(kind)));
        }
    }

    private void SelectGraphItem(int item)
    {
        if ((view.Graph.Source?.Count ?? 0) > item)
        {
            view.Graph.SelectedItem = item;
            view.Graph.EnsureSelectedItemVisible();
        }
    }

    private ScreenModel CreateModel() =>
        ScreenModelFactory.Create(
            host.Workspace,
            ActiveSession(),
            keys.Mode,
            keys.PendingText,
            FirstNotEmpty(workspaceActions.Message, router.Message, statuses.Log.Latest?.Text ?? string.Empty),
            view.Graph.SelectedItem,
            TabLabelFactory.Create(host.Workspace, tabId => effects.SessionOf(tabId) is { IsLoading: true, IsPending: false }),
            picker.State,
            homeDirectory)
        with
        {
            MessageSeverity = workspaceActions.Message.Length + router.Message.Length > 0 ? StatusSeverity.Info : statuses.Log.Latest?.Severity ?? StatusSeverity.Info,
            Hints = PaneHints.For(host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph),
            SearchLine = prompts.Select(prompt => prompt.StatusLine).FirstOrDefault(line => line.Length > 0) ?? string.Empty,
        };

    private static string FirstNotEmpty(params string[] texts) => texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;

    // app.Invoke runs at once when called on the UI thread; a zero timeout runs the work in a later loop iteration so keys are handled in between.
    private static Action<Action> DeferToNextIteration(IApplication app) => work => app.AddTimeout(
        TimeSpan.Zero,
        () =>
        {
            work();
            return false;
        });

    private IReadOnlyList<IPromptHandler> CreatePrompts(IApplication app)
    {
        var handle = new GraphViewHandle(
            ActiveSession,
            () => view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1,
            row =>
            {
                view.Graph.SelectedItem = row + ScreenLayout.ReservedLeadingRows;
                view.Graph.EnsureSelectedItemVisible();
            },
            keys.SetMode,
            () =>
            {
                view.Graph.SetNeedsDraw();
                view.RefreshChrome();
            });
        return
        [
            // The prompt that owns Insert mode must see Esc and Enter first, so the ref prompt comes before the search.
            new RefJumpController(handle, () => host.Workspace.ActiveTab?.Refs ?? []),
            new SearchController(handle, DeferToNextIteration(app)),
        ];
    }

    private StyledLine DetailTitle(int width, GraphGlyphSet glyphs)
    {
        var content = ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;
        var isFocused = view.Detail.HasFocus;
        return DetailPaneRenderer.RenderTitle(content, isFocused, width, glyphs);
    }

    private void OnGraphChanged()
    {
        foreach (var prompt in prompts)
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

        view.Detail.Source = current?.DetailSource;
        diffPane.OnTabSwitched(previous, current);
        if (current is not null)
        {
            current.Details.Changed += RefreshDetail;
        }

        RefreshDetail();
    }

    private bool HandleKey(Key key)
    {
        var view = picker.IsOpen && !overlays.State.IsOpen ? DefaultKeymap.PickerView : overlays.ViewName;
        return keys.Handle(key, view) || (picker.IsOpen && !overlays.State.IsOpen);
    }

    private void OnTextKey(KeyEvent keyEvent)
    {
        if (!picker.IsOpen || keys.Mode != KeyMode.Insert)
        {
            return;
        }

        // The picker's query is plain state, not a text widget: it takes printable keys and edits itself with Backspace (some terminals send it as Ctrl-h).
        if (keyEvent.IsPrintable)
        {
            picker.Apply(new TypeTextCommand(keyEvent.Name));
        }
        else if (keyEvent is { Name: KeyNames.Backspace, Modifiers: KeyModifiers.None or KeyModifiers.Ctrl })
        {
            picker.Apply(new BackspaceCommand());
        }
    }

    private void OnPickerChanged()
    {
        if (!overlays.State.IsOpen && picker.IsOpen != (keys.Mode == KeyMode.Insert))
        {
            keys.SetMode(picker.IsOpen ? KeyMode.Insert : KeyMode.Normal);
        }

        view.ShowPicker(picker.IsOpen);
        if (!picker.IsOpen)
        {
            // Hiding the graph moved the toolkit focus: put it back on the pane the tab's state names.
            appliedFocus = (PaneKind)(-1);
            ApplyFocus();
        }

        view.RefreshChrome();
    }

    // A click highlights the row; the double-click's "open" comes from the mouse map as picker.accept.
    private void OnPickerRowClicked(int index)
    {
        if (picker.State is { } state)
        {
            picker.Apply(new MoveHighlightCommand(index - state.Highlight));
        }
    }

    private void OnSelectionChanged()
    {
        var session = ActiveSession();
        var tabId = host.Workspace.ActiveTabId;
        if (session is null || tabId is null || view.Graph.SelectedItem is not { } item)
        {
            return;
        }

        // While the binder swaps or restores the list, the tab's saved selection must not be overwritten, but the details still follow.
        var record = !binder.IsSuspended;
        var commitIndex = GraphListSource.CommitIndexOf(item);
        if (commitIndex >= 0 && commitIndex < session.Graph.Count)
        {
            var entry = session.Graph[commitIndex];
            if (record)
            {
                host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(entry.Id)));
            }

            // Cheap on every move: the store shows "loading" at once and only asks git once the selection settles.
            session.Details.Select(entry.Id);
            RefreshDetail();
        }

        if (record)
        {
            host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(view.Graph.Viewport.Y)));
        }
    }

    private void RefreshDetail()
    {
        var session = ActiveSession();
        if (session is null)
        {
            return;
        }

        var commitIndex = view.Graph.SelectedItem is { } item ? GraphListSource.CommitIndexOf(item) : -1;
        var entry = commitIndex >= 0 && commitIndex < session.Graph.Count ? session.Graph[commitIndex] : null;
        var previous = session.DetailSource.Content;
        var content = DetailContentBuilder.Build(entry, session.Details.Current);
        session.DetailSource.Update(content);

        // A different commit starts at the top; the same commit whose details just arrived keeps the user's place.
        var selected = content.ShortHash == previous.ShortHash ? view.Detail.SelectedItem ?? 0 : 0;
        view.Detail.SelectedItem = content.Rows.Count == 0 ? null : Math.Min(selected, content.Rows.Count - 1);
        view.Detail.EnsureSelectedItemVisible();
        view.Detail.SetNeedsDraw();
        view.RefreshChrome();
        diffPane.FollowSelection();
    }
}
