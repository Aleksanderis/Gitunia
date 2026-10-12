using Gitunia.Core.Graph;
using Gitunia.Core.Keymap;
using Gitunia.Core.Persistence;
using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Platform;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Rendering;
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
    private OverlayController overlays = null!;
    private WhichKeyTimer whichKeyTimer = null!;
    private PickerController picker = null!;
    private WorkspaceActions workspaceActions = null!;
    private GraphTabBinder binder = null!;
    private string homeDirectory = string.Empty;
    private string startupNotice = string.Empty;

    /// <summary>Runs the UI until the user quits.</summary>
    /// <param name="location">A repository to open on top of the restored session, null for none.</param>
    public int Run(RepositoryLocation? location)
    {
        var glyphs = GraphGlyphSet.For(capabilities.Unicode);
        homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var store = new SessionStore(platform.StateDirectory);
        var restored = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        startupNotice = restored.Detail ?? string.Empty;

        using var app = Application.Create();
        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        app.Init();

        host = new WorkspaceHost(Workspace.Empty);
        effects = new EffectRunner(runner, host, action => app.Invoke(action), glyphs);
        view = new MainView(CreateModel, width => DetailTitle(width, glyphs), glyphs);
        router = new ActionRouter(host, view.Graph, view.Detail, ActiveSession, app.RequestStop);
        picker = new PickerController(runner, host, homeDirectory, action => app.Invoke(action));
        workspaceActions = new WorkspaceActions(host, picker, app.RequestStop);
        binder = new GraphTabBinder(host, effects, view.Graph, new GraphListSource(new CommitGraph(), glyphs), view.RefreshChrome);
        binder.Switched += OnTabSwitched;
        var engine = new KeymapEngine(DefaultKeymap.Create());
        keys = new KeyController(engine, (action, count) => RouteAction(action, count));
        whichKeyTimer = new WhichKeyTimer(OverlayController.WhichKeyDelay, action => app.Invoke(action));
        overlays = new OverlayController(engine, keys, whichKeyTimer, RouteAction, () => view.ScreenSize, glyphs, () => router.ViewName);
        overlays.Changed += RefreshOverlay;

        host.Changed += view.RefreshChrome;
        host.Changed += binder.Sync;
        keys.PendingChanged += view.RefreshChrome;
        keys.TextKeyPressed += OnTextKey;
        picker.Changed += OnPickerChanged;
        view.Graph.ValueChanged += (_, _) => OnSelectionChanged();
        view.TabClicked += OnTabClicked;
        view.PickerRowClicked += OnPickerRowClicked;
        app.Keyboard.KeyDown += (_, key) => key.Handled = HandleKey(key);

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

        using var persistence = new SessionPersistence(store, host, SaveDebounce);
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

    private TabSession? ActiveSession() => host.Workspace.ActiveTabId is { } id ? effects.SessionOf(id) : null;

    private ScreenModel CreateModel() =>
        ScreenModelFactory.Create(
            host.Workspace,
            ActiveSession(),
            keys.Mode,
            keys.PendingText,
            FirstNotEmpty(workspaceActions.Message, router.Message, startupNotice),
            view.Graph.SelectedItem,
            TabLabelFactory.Create(host.Workspace, tabId => effects.SessionOf(tabId)?.IsLoading ?? false),
            picker.State,
            homeDirectory);

    private static string FirstNotEmpty(params string[] texts) => texts.FirstOrDefault(text => text.Length > 0) ?? string.Empty;

    private StyledLine DetailTitle(int width, GraphGlyphSet glyphs)
    {
        var content = ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;
        var isFocused = view.Detail.HasFocus;
        return DetailPaneRenderer.RenderTitle(content, isFocused, width, glyphs);
    }

    private void OnTabSwitched(TabSession? previous, TabSession? current)
    {
        if (previous is not null)
        {
            previous.Details.Changed -= RefreshDetail;
        }

        view.Detail.Source = current?.DetailSource;
        if (current is not null)
        {
            current.Details.Changed += RefreshDetail;
        }

        RefreshDetail();
    }

    private bool HandleKey(Key key)
    {
        startupNotice = string.Empty;
        var handled = keys.Handle(key, picker.IsOpen && !overlays.State.IsOpen ? DefaultKeymap.PickerView : overlays.ViewName);
        if (handled || !picker.IsOpen || overlays.State.IsOpen)
        {
            return handled;
        }

        // The picker's query is plain state, not a text widget: it edits itself with Backspace (some terminals send it as Ctrl-h) and swallows other keys.
        if (KeyTranslator.TryTranslate(key, out var keyEvent) && keyEvent is { Name: KeyNames.Backspace, Modifiers: KeyModifiers.None or KeyModifiers.Ctrl })
        {
            picker.Apply(new BackspaceCommand());
        }

        return true;
    }

    private void OnTextKey(KeyEvent keyEvent)
    {
        if (picker.IsOpen && keys.Mode == KeyMode.Insert)
        {
            picker.Apply(new TypeTextCommand(keyEvent.Name));
        }
    }

    private void OnPickerChanged()
    {
        if (!overlays.State.IsOpen && picker.IsOpen != (keys.Mode == KeyMode.Insert))
        {
            keys.SetMode(picker.IsOpen ? KeyMode.Insert : KeyMode.Normal);
        }

        view.ShowPicker(picker.IsOpen);
        view.RefreshChrome();
    }

    private void OnTabClicked(int number)
    {
        picker.Close();
        host.Dispatch(new GoToTabCommand(number));
    }

    private void OnPickerRowClicked(int index, bool isDoubleClick)
    {
        if (picker.State is not { } state)
        {
            return;
        }

        picker.Apply(new MoveHighlightCommand(index - state.Highlight));
        if (isDoubleClick)
        {
            picker.Accept();
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
    }
}
