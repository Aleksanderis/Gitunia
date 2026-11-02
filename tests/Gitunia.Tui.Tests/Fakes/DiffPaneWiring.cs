using Gitunia.App;
using Gitunia.App.DiffPane;
using Gitunia.App.Tabs;
using Gitunia.Core;
using Gitunia.Core.DiffPane;
using Gitunia.Core.Effects;
using Gitunia.Core.Shell;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Clipboard;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Views;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// The diff pane as the application wires it, for one tab session and two lists: the view and the input on a dispatcher
/// whose diff effects run on the session's stores and whose status notices are collected (the runner does both in the
/// application). The screen size goes to the state like the terminal layer reports it.
/// </summary>
public sealed class DiffPaneWiring : IActiveSessions, IPaneChrome, ITabSessions, IPaneLists, IDisposable
{
    private readonly Dispatcher _host;
    private readonly TerminalTabSession _session;
    private readonly ListView _detailList;
    private readonly ListView _diffList;
    private readonly DiffPaneEffectHandler _effects;
    private ScreenSize _screen;

    public DiffPaneWiring(
        Dispatcher host,
        TerminalTabSession session,
        ListView detailList,
        ListView diffList,
        ScreenSize screen,
        ClipboardCopier clipboard)
    {
        _host = host;
        _session = session;
        _detailList = detailList;
        _diffList = diffList;
        _effects = new DiffPaneEffectHandler(this);
        host.EffectRaised += OnEffect;
        View = new DiffPaneView(host, this, this, this, clipboard);
        Input = new DiffPaneInput(host, this, this, View);
        Resize(screen);
        View.OnTabSwitched(null, session);
        detailList.ValueChanged += (_, _) => Input.OnDetailCursorMoved();
    }

    public DiffPaneView View { get; }

    public DiffPaneInput Input { get; }

    /// <summary>The status notices the reducers asked for, oldest first.</summary>
    public List<string> Notices { get; } = [];

    /// <summary>How often a relayout was asked for.</summary>
    public int Relayouts { get; private set; }

    public ScreenSize ScreenSize => _screen;

    /// <summary>The terminal has this size now.</summary>
    public void Resize(ScreenSize screen)
    {
        _screen = screen;
        _host.Dispatch(
            new ScreenResizedCommand(new ScreenExtent(screen.Width, screen.Height), _host.Clock.GetUtcNow()));
    }

    // ReSharper disable once ReturnTypeCanBeNotNullable : fake shared with Gitunia.App.Tests by link: a member counts as unused when only the other project uses it
    TerminalTabSession? IActiveSessions.ActiveSession() => _session;

    public TabSession? Find(TabId tabId) => tabId == _session.TabId ? _session : null;

    public ListView ListOf(PaneKind pane) => pane == PaneKind.Detail ? _detailList : _diffList;

    public void Relayout() => Relayouts++;

    public void RefreshChrome()
    {
    }

    public void Dispose() => _host.EffectRaised -= OnEffect;

    private void OnEffect(AppEffect effect)
    {
        if (effect is DiffPaneEffect diffEffect)
        {
            _effects.Handle(diffEffect);
        }
        else if (effect is PostStatusEffect post)
        {
            Notices.Add(post.Text);
        }
    }
}
