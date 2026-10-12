using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Platform;
using Gitunia.Tui.Input;
using Gitunia.Tui.Loading;
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

    public int Run(RepositoryLocation location)
    {
        var glyphs = GraphGlyphSet.For(capabilities.Unicode);
        using var app = Application.Create();
        Application.MaximumIterationsPerSecond = IterationsPerSecond;
        app.Init();

        host = new WorkspaceHost(Workspace.Empty);
        effects = new EffectRunner(runner, host, action => app.Invoke(action), glyphs);
        view = new MainView(CreateModel, width => DetailTitle(width, glyphs), glyphs);
        router = new ActionRouter(host, view.Graph, view.Detail, ActiveSession, app.RequestStop);
        keys = new KeyController(new KeymapEngine(DefaultKeymap.Create()), router.Execute);

        host.Changed += view.RefreshChrome;
        keys.PendingChanged += view.RefreshChrome;
        view.Graph.ValueChanged += (_, _) => OnSelectionChanged();
        app.Keyboard.KeyDown += (_, key) => key.Handled = keys.Handle(key, router.ViewName);

        host.Dispatch(new OpenRepositoryCommand(location));
        var session = ActiveSession();
        if (session is not null)
        {
            view.Graph.Source = session.Source;
            view.Detail.Source = session.DetailSource;
            session.Details.Changed += RefreshDetail;
            session.Changed += OnSessionChanged;
        }

        view.Graph.SetFocus();
        app.Run(view.Window);
        return 0;
    }

    public void Dispose() => view?.Dispose();

    private TabSession? ActiveSession() => host.Workspace.ActiveTabId is { } id ? effects.SessionOf(id) : null;

    private ScreenModel CreateModel() =>
        ScreenModelFactory.Create(host.Workspace, ActiveSession(), keys.Mode, keys.PendingText, router.Message, view.Graph.SelectedItem);

    private StyledLine DetailTitle(int width, GraphGlyphSet glyphs)
    {
        var content = ActiveSession()?.DetailSource.Content ?? DetailContent.Empty;
        var isFocused = view.Detail.HasFocus;
        return DetailPaneRenderer.RenderTitle(content, isFocused, width, glyphs);
    }

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
            var entry = session.Graph[commitIndex];
            host.Dispatch(new TabCommand(tabId.Value, new SelectCommitCommand(entry.Id)));

            // Cheap on every move: the store shows "loading" at once and only asks git once the selection settles.
            session.Details.Select(entry.Id);
            RefreshDetail();
        }

        host.Dispatch(new TabCommand(tabId.Value, new ScrollToCommand(view.Graph.Viewport.Y)));
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
