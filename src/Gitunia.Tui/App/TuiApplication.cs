using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Platform;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering;
using Gitunia.Tui.Views;
using Terminal.Gui.App;

namespace Gitunia.Tui.App;

/// <summary>
/// Wires the Terminal.Gui shell to the state layer: keys go through the keymap engine, actions become workspace
/// commands, git results come back on the UI thread. This is the only place that knows both worlds.
/// </summary>
public sealed class TuiApplication(GitRunner runner, ConsoleCapabilities capabilities) : IDisposable
{
    // Terminal.Gui's default of 25 iterations per second gave ~34 ms key latency; see docs/decisions.md.
    private const ushort IterationsPerSecond = 100;

    private WorkspaceHost host = null!;
    private EffectRunner effects = null!;
    private MainView view = null!;
    private ActionRouter router = null!;
    private KeyController keys = null!;
    private SidebarActionHandler sidebar = null!;
    private PaneKind appliedFocus = PaneKind.Graph;

    public int Run(RepositoryLocation location)
    {
        var glyphs = GraphGlyphSet.For(capabilities.Unicode);
        using var app = Application.Create();
        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        app.Init();

        host = new WorkspaceHost(Workspace.Empty);
        effects = new EffectRunner(runner, host, action => app.Invoke(action), glyphs);
        var sidebarSource = new SidebarListSource(glyphs);
        sidebar = new SidebarActionHandler(
            host,
            sidebarSource,
            ActiveSession,
            () => view.Sidebar.Viewport.Height,
            () => ScreenLayout.SidebarWidth(view.Window.Viewport.Width) > 0,
            SelectGraphItem,
            mode => keys.SetMode(mode));
        view = new MainView(CreateModel, glyphs, sidebarSource, () => sidebar.Chrome);
        router = new ActionRouter(host, view.Graph, ActiveSession, app.RequestStop, sidebar);
        keys = new KeyController(new KeymapEngine(DefaultKeymap.Create()), router.Execute);
        var presenter = new SidebarPresenter(host, view.Sidebar, sidebarSource);

        host.Changed += view.RefreshChrome;
        host.Changed += ApplyFocus;
        keys.PendingChanged += view.RefreshChrome;
        keys.TextTyped += sidebar.OnText;
        keys.Erased += sidebar.OnErase;
        presenter.Clicked += sidebar.ActivateCursor;
        view.Graph.ValueChanged += (_, _) => OnSelectionChanged();
        view.Graph.HasFocusChanged += (_, _) => OnPaneFocused(view.Graph, PaneKind.Graph);
        view.Sidebar.HasFocusChanged += (_, _) => OnPaneFocused(view.Sidebar, PaneKind.Sidebar);
        app.Keyboard.KeyDown += (_, key) => key.Handled = keys.Handle(key, FocusedViewName());

        host.Dispatch(new OpenRepositoryCommand(location));
        var session = ActiveSession();
        if (session is not null)
        {
            view.Graph.Source = session.Source;
            session.Changed += OnSessionChanged;
        }

        view.Graph.SetFocus();
        app.Run(view.Window);
        return 0;
    }

    public void Dispose() => view?.Dispose();

    private TabSession? ActiveSession() => host.Workspace.ActiveTabId is { } id ? effects.SessionOf(id) : null;

    private string FocusedViewName() => host.Workspace.ActiveTab?.View.FocusedPane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Detail => DefaultKeymap.DetailView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        _ => DefaultKeymap.GraphView,
    };

    // The state decides which pane has focus; the views follow it.
    private void ApplyFocus()
    {
        var wanted = host.Workspace.ActiveTab?.View.FocusedPane ?? PaneKind.Graph;
        if (wanted == appliedFocus)
        {
            return;
        }

        appliedFocus = wanted;
        (wanted == PaneKind.Sidebar ? view.Sidebar : view.Graph).SetFocus();
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
        ScreenModelFactory.Create(host.Workspace, ActiveSession(), keys.Mode, keys.PendingText, router.Message, view.Graph.SelectedItem);

    private void OnSessionChanged()
    {
        if (view.Graph.SelectedItem is null && (view.Graph.Source?.Count ?? 0) > 0)
        {
            view.Graph.SelectedItem = 0;
        }

        view.Graph.SetNeedsDraw();
        view.RefreshChrome();
    }

    private void OnSelectionChanged()
    {
        var session = ActiveSession();
        var tabId = host.Workspace.ActiveTabId;
        if (session is null || tabId is null || view.Graph.SelectedItem is not { } item)
        {
            return;
        }

        var commitIndex = GraphListSource.CommitIndexOf(item);
        if (commitIndex >= 0 && commitIndex < session.Graph.Count)
        {
            host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(session.Graph[commitIndex].Id)));
        }

        host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(view.Graph.Viewport.Y)));
    }
}
